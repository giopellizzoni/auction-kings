namespace AuctionKings.Domain.Enums;

/// <summary>
/// Authorization role carried in the JWT <c>role</c> claim. The names must match the API's authorization policies.
/// </summary>
public enum UserRole
{
    Bidder = 0,
    Admin = 1,
}
