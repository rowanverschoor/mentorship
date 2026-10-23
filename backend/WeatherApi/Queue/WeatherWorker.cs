using Polly.CircuitBreaker;
using WeatherApi.Realtime;
using WeatherApi.Services;

namespace WeatherApi.Queue;

// Runs in the background for the app's whole lifetime and handles one job at a time.
public class WeatherWorker(
    IWeatherQueue queue, IWeatherNotifier notifier, IServiceScopeFactory scopes, ILogger<WeatherWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            WeatherUpdate update;
            try
            {
                using var scope = scopes.CreateScope();
                var weather = scope.ServiceProvider.GetRequiredService<IWeatherService>();

                update = new WeatherUpdate(job.Id, await weather.GetAsync(job.Latitude, job.Longitude), null);
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Weather job {Id} failed", job.Id);
                var error = e is BrokenCircuitException
                    ? "Weather provider is temporarily unavailable."
                    : "Weather provider failed.";
                update = new WeatherUpdate(job.Id, null, error);
            }

            await notifier.NotifyAsync(job.User, update, stoppingToken);
        }
    }
}

