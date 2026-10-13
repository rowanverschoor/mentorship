using System.Threading.RateLimiting;

namespace WeatherApi.Resilience;

public static class RateLimitExtensions
{
    public const string PolicyName = "weather";

    // Protects OUR api (and our provider quota) from callers that hit it too often.
    // Each client IP gets its own counter: 20 requests per minute, then HTTP 429.
    public static IServiceCollection AddWeatherRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });
}
