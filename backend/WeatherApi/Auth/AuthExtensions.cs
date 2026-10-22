using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using WeatherApi.Resilience;

namespace WeatherApi.Auth;

public record LoginRequest(string Username, string Password);

public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations().ValidateOnStart();
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IUserValidator, ConfiguredUserValidator>();

        // Validates the bearer token on every request: signature, issuer, audience, expiry.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) => bearer.TokenValidationParameters = new()
            {
                ValidIssuer = jwt.Value.Issuer,
                ValidAudience = jwt.Value.Audience,
                IssuerSigningKey = jwt.Value.SigningKey,
            });

        return services.AddAuthorization();
    }

    public static void MapLogin(this WebApplication app) =>
        app.MapPost("/login", (LoginRequest req, IUserValidator users, ITokenService tokens) =>
            users.IsValid(req.Username, req.Password)
                ? Results.Ok(new { token = tokens.Create(req.Username) })
                : Results.Unauthorized()).RequireRateLimiting(RateLimitExtensions.LoginPolicyName);
}
