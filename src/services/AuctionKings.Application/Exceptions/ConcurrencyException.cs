namespace AuctionKings.Application.Exceptions;

public sealed class ConcurrencyException(Exception? innerException = null)
    : Exception("A concurrency conflict occurred.", innerException);
