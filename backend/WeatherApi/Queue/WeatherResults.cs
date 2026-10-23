using System.Collections.Concurrent;
using WeatherApi.Models;

namespace WeatherApi.Queue;

public record JobResult(string User, WeatherResult? Weather = null, string? Error = null)
{
    public bool Done => Weather is not null || Error is not null;
}

// Where the worker leaves results until the client asks for them.
// Temporary: SignalR will push results instead, and this goes away.
public class WeatherResults
{
    private readonly ConcurrentDictionary<Guid, JobResult> _results = new();

    public void Set(Guid id, JobResult result) => _results[id] = result;
    public JobResult? Get(Guid id) => _results.GetValueOrDefault(id);
    public void Remove(Guid id) => _results.TryRemove(id, out _);
}