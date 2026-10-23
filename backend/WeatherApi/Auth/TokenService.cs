using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace WeatherApi.Auth;

// Contract for issuing a token. The login endpoint depends on this, not on JWT details.
public interface ITokenService
{
    string Create(string username);
}

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public string Create(string username)
    {
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            // Name is for our endpoints, NameIdentifier is how SignalR's Clients.User() finds the user.
            claims: [new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.NameIdentifier, username)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(_jwt.SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
