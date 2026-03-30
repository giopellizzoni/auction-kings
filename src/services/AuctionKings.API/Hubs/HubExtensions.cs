using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace AuctionKings.API.Hubs;

internal static class HubExtensions
{
    internal static Guid? GetUserId(this HubCallerContext context)
    {
        var value = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
