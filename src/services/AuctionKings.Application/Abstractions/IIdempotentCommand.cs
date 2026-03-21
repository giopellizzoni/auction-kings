namespace AuctionKings.Application.Abstractions;

public interface IIdempotentCommand
{
    string IdempotencyKey { get; }
}
