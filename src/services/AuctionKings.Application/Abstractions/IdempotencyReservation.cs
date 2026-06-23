namespace AuctionKings.Application.Abstractions;

public enum IdempotencyReservationOutcome
{
    Reserved,
    InFlight,
    Completed
}

public sealed class IdempotencyReservation
{
    public IdempotencyReservationOutcome Outcome { get; }
    public string? StoredResponse { get; }

    private IdempotencyReservation(IdempotencyReservationOutcome outcome, string? storedResponse = null)
    {
        Outcome = outcome;
        StoredResponse = storedResponse;
    }

    public static IdempotencyReservation Reserved => new(IdempotencyReservationOutcome.Reserved);
    public static IdempotencyReservation InFlight => new(IdempotencyReservationOutcome.InFlight);
    public static IdempotencyReservation Completed(string storedResponse) =>
        new(IdempotencyReservationOutcome.Completed, storedResponse);
}
