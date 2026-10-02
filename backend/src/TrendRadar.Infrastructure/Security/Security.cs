using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auth;
using TrendRadar.Domain.Users;

namespace TrendRadar.Infrastructure.Security;

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

internal sealed class JwtAccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider time) : IAccessTokenIssuer
{
    public AccessToken Issue(User user)
    {
        var o = options.Value;
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(o.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: o.Issuer,
            audience: o.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.DisplayName),
                new Claim("role", user.Role.ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions o) => new(Encoding.UTF8.GetBytes(o.JwtSecret));
}
