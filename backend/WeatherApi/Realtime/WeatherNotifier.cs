using Microsoft.AspNetCore.SignalR;
using WeatherApi.Models;

namespace WeatherApi.Realtime;

// What a user is told when their job is finished: the weather, or an error.
public record WeatherUpdate(Guid Id, WeatherResult? Weather, string? Error);

// Contract for delivering a finished job. The worker depends on this, not on SignalR,
// so the delivery mechanism can be swapped without touching the worker.
public interface IWeatherNotifier
{
    Task NotifyAsync(string user, WeatherUpdate update, CancellationToken ct);
}

// Pushes to every open connection of that user (matched on the NameIdentifier claim); nobody else gets it.
public class SignalRWeatherNotifier(IHubContext<WeatherHub> hub) : IWeatherNotifier
{
    public Task NotifyAsync(string user, WeatherUpdate update, CancellationToken ct) =>
        hub.Clients.User(user).SendAsync("weather", update, ct);
}
