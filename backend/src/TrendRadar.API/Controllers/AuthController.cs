using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auth;

namespace TrendRadar.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, IUserStore users, IOptions<AuthOptions> options) : ControllerBase
{
    public const string RefreshCookie = "tr_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.LoginRateLimit)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Email, request.Password, ct);
        return Respond(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], ct);
        return Respond(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(null));
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await users.FindByIdAsync(User.GetUserId(), ct);
        return user is { IsActive: true } ? UserDto.From(user) : Unauthorized();
    }

    private ActionResult<AuthResponse> Respond(AuthResult result)
    {
        if (result.Tokens is not { } t)
        {
            Response.Cookies.Delete(RefreshCookie, CookieOptions(null));
            return Problem(title: result.Error, statusCode: StatusCodes.Status401Unauthorized);
        }

        Response.Cookies.Append(RefreshCookie, t.RefreshToken, CookieOptions(t.RefreshTokenExpiresAt));
        return new AuthResponse(t.AccessToken.Value, t.AccessToken.ExpiresAt, UserDto.From(t.User));
    }

    private CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = options.Value.SecureCookies,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
    };
}
