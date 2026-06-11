using System.Security.Claims;

namespace AuctionKings.API.Auth;

internal static class ClaimsPrincipalExtensions
{
    internal static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
