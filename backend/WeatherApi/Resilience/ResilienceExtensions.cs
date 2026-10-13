using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace WeatherApi.Resilience;

public static class ResilienceExtensions
{
    // Wraps every outgoing call to the weather provider in a pipeline.
    // Order matters: the outermost strategy runs first.
    //   total timeout -> retry -> circuit breaker -> per-attempt timeout -> HTTP call
    //
    // "Failure" for retry and breaker means: network error, timeout, 5xx, 408 or 429.
    // A 400 is our fault (bad input), so it is never retried and never trips the breaker.
    public static IHttpResiliencePipelineBuilder AddWeatherResilience(this IHttpClientBuilder builder) =>
        builder.AddResilienceHandler("weather", pipeline =>
        {
            // Give up on the whole operation (all retries included) after 20s.
            pipeline.AddTimeout(TimeSpan.FromSeconds(20));

            // Retry limit: at most 3 extra attempts, waiting 0.5s, 1s, 2s (+ jitter).
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });

            // Circuit breaker: if half of the calls fail (with at least 5 calls in 30s),
            // stop calling the provider for 15s and fail immediately instead.
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
            });

            // One slow attempt may take at most 5s before it counts as a failure.
            pipeline.AddTimeout(TimeSpan.FromSeconds(5));
        });
}
