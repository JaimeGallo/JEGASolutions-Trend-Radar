namespace TrendRadar.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string JwtSecret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "trend-radar";

    public string Audience { get; set; } = "trend-radar";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    public bool SecureCookies { get; set; } = true;

    public const int MinPasswordLength = 12;
}
