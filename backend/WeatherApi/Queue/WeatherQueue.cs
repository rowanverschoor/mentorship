using System.Threading.Channels;

namespace WeatherApi.Queue;

public record WeatherJob(Guid Id, string User, double Latitude, double Longitude);

// Contract for handing work to the worker. The endpoint depends on this, not on Channel,
// so it can be swapped for RabbitMQ without touching the endpoint.
public interface IWeatherQueue
{
    bool TryEnqueue(WeatherJob job);
    IAsyncEnumerable<WeatherJob> ReadAllAsync(CancellationToken ct);
}

// In-memory queue. Bounded, so a flood of requests is rejected instead of filling memory.
public class ChannelWeatherQueue : IWeatherQueue
{
    private readonly Channel<WeatherJob> _channel =
        Channel.CreateBounded<WeatherJob>(new BoundedChannelOptions(100) { SingleReader = true });

    public bool TryEnqueue(WeatherJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<WeatherJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}