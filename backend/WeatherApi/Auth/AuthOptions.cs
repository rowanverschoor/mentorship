using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WeatherApi.Auth;

// Bound from the "Jwt" config section and validated at startup, so a missing key fails fast.
public class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; init; } = "";
    [Required] public string Audience { get; init; } = "";
    [Required, MinLength(32)] public string Key { get; init; } = "";

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public class AuthOptions
{
    public const string Section = "Auth";

    public string User { get; init; } = "";
    public string Password { get; init; } = "";
}
