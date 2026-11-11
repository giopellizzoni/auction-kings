using System.Text.Json;
using System.Text.Json.Serialization;
using AuctionKings.Shared.Common;

namespace AuctionKings.Application.Behaviors;

/// <summary>
/// Handles <see cref="Result"/> and <see cref="Result{TValue}"/> which have no public
/// constructor and therefore cannot be round-tripped by the default System.Text.Json
/// deserializer. Reads public properties and reconstructs via factory methods.
/// </summary>
/// <remarks>
/// Deliberately a top-level, non-generic type: when the converters were nested inside the
/// generic <c>IdempotencyBehavior&lt;TRequest, TResponse&gt;</c>, <c>typeof(GenericResultConverter&lt;&gt;)</c>
/// silently carried the outer type parameters too (arity 3), so <c>MakeGenericType(valueType)</c>
/// threw on every successful idempotent command.
/// </remarks>
internal sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(Result)
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
