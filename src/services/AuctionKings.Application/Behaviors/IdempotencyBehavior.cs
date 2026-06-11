using System.Text.Json;
using System.Text.Json.Serialization;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Exceptions;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly JsonSerializerOptions SerializerOptions = BuildOptions();

    private readonly IIdempotencyService _idempotencyService;

    public IdempotencyBehavior(IIdempotencyService idempotencyService)
    {
        _idempotencyService = idempotencyService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request is not IIdempotentCommand idempotentCommand)
        {
            return await next(cancellationToken);
        }

        var key = idempotentCommand.IdempotencyKey;

        // 1. Redis fast path — cheapest check first.
        var cached = await _idempotencyService.GetCachedResponseAsync(key, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<TResponse>(cached, SerializerOptions)!;
        }

        // 2. Atomic DB reservation.
        var reservation = await _idempotencyService.TryReserveAsync(key, cancellationToken);

        switch (reservation.Outcome)
        {
            case IdempotencyReservationOutcome.Completed:
                return JsonSerializer.Deserialize<TResponse>(reservation.StoredResponse!, SerializerOptions)!;

            case IdempotencyReservationOutcome.InFlight:
                throw new DuplicateCommandException(key);
        }

        // 3. Execute the handler; release reservation on any failure.
        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch
        {
            await _idempotencyService.ReleaseAsync(key, CancellationToken.None);
            throw;
        }

        // 4. Persist response so future duplicates get a replay.
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await _idempotencyService.CompleteAsync(key, responseJson, CancellationToken.None);

        return response;
    }

    private static JsonSerializerOptions BuildOptions()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opts.Converters.Add(new ResultConverterFactory());
        return opts;
    }

    /// <summary>
    /// Handles <see cref="Result"/> and <see cref="Result{TValue}"/> which have no public
    /// constructor and therefore cannot be round-tripped by the default System.Text.Json
    /// deserializer. Reads public properties and reconstructs via factory methods.
    /// </summary>
    private sealed class ResultConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert == typeof(Result)
            || (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>));

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert == typeof(Result))
            {
                return new NonGenericResultConverter();
            }

            var valueType = typeToConvert.GetGenericArguments()[0];
            var converterType = typeof(GenericResultConverter<>).MakeGenericType(valueType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    private sealed class NonGenericResultConverter : JsonConverter<Result>
    {
        public override Result? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var isSuccess = root.GetProperty("isSuccess").GetBoolean();
            if (isSuccess)
            {
                return Result.Success();
            }

            var error = root.GetProperty("error").Deserialize<Error>(options)!;
            return Result.Failure(error);
        }

        public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("isSuccess", value.IsSuccess);
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, value.Error, options);
            writer.WriteEndObject();
        }
    }

    private sealed class GenericResultConverter<TValue> : JsonConverter<Result<TValue>>
    {
        public override Result<TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var isSuccess = root.GetProperty("isSuccess").GetBoolean();
            if (isSuccess)
            {
                var value = root.GetProperty("value").Deserialize<TValue>(options)!;
                return Result.Success(value);
            }

            var error = root.GetProperty("error").Deserialize<Error>(options)!;
            return Result.Failure<TValue>(error);
        }

        public override void Write(Utf8JsonWriter writer, Result<TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("isSuccess", value.IsSuccess);
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, value.Error, options);
            if (value.IsSuccess)
            {
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, value.Value, options);
            }
            writer.WriteEndObject();
        }
    }
}
