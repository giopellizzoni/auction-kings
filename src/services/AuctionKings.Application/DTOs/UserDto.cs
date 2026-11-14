namespace AuctionKings.Application.DTOs;

/// <summary>A user as returned by the API. <c>Role</c> is the name carried in the JWT role claim (e.g. <c>Bidder</c>).</summary>
public sealed record UserDto(Guid Id, string Email, string DisplayName, string Role, DateTime CreatedAt);
