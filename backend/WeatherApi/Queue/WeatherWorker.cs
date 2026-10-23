using Polly.CircuitBreaker;
using WeatherApi.Services;

namespace WeatherApi.Queue;

// Runs in the background for the app's whole lifetime and handles one job at a time.
public class WeatherWorker(
    IWeatherQueue queue, WeatherResults results, IServiceScopeFactory scopes, ILogger<WeatherWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                // The worker lives forever, the weather client should not: get a fresh one per job.
                using var scope = scopes.CreateScope();
                var weather = scope.ServiceProvider.GetRequiredService<IWeatherService>();

                results.Set(job.Id, new JobResult(job.User, Weather: await weather.GetAsync(job.Latitude, job.Longitude)));
            }
            catch (Exception e)
            {
                // Never let one failed job escape: an unhandled exception here stops the whole app.
                log.LogWarning(e, "Weather job {Id} failed", job.Id);
                var error = e is BrokenCircuitException
                    ? "Weather provider is temporarily unavailable."
                    : "Weather provider failed.";
                results.Set(job.Id, new JobResult(job.User, Error: error));
            }
        }
    }
}
