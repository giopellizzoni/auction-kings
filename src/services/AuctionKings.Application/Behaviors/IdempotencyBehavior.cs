using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Exceptions;
using MediatR;

namespace AuctionKings.Application.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
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

        if (await _idempotencyService.ExistsAsync(key, cancellationToken))
        {
            throw new DuplicateCommandException(key);
        }

        var response = await next(cancellationToken);

        await _idempotencyService.StoreAsync(key, cancellationToken);

        return response;
    }
}
