using WeatherApi.Models;

namespace WeatherApi.Services;

// Contract for getting weather. The endpoint depends on this, not on any
// specific provider, so the provider can be swapped without touching the endpoint.
public interface IWeatherService
{
    Task<WeatherResult> GetAsync(double latitude, double longitude);
}