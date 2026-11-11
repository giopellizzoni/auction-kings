using System.Text.Json;
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

        // 4. A failed Result has produced no side effects, and some failures are transient
        //    (e.g. Auction.ConcurrencyConflict). Caching them would make a client retrying with the
        //    same key receive the stale failure for 24h, so release the key and let it re-execute.
        if (response is Result { IsFailure: true })
        {
            await _idempotencyService.ReleaseAsync(key, CancellationToken.None);
            return response;
        }

        // 5. Persist the successful response so future duplicates get a replay.
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await _idempotencyService.CompleteAsync(key, responseJson, CancellationToken.None);

        return response;
    }

    private static JsonSerializerOptions BuildOptions()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opts.Converters.Add(new ResultJsonConverterFactory());
        return opts;
    }
}
