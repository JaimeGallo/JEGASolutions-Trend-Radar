using System.Security.Claims;

namespace TrendRadar.API;

internal static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Token sin 'sub'."));
}
