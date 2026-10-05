using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WeatherApi.Models;

namespace WeatherApi.Services;

public class ApiNinjasWeatherService(HttpClient http) : IWeatherService
{
    public async Task<WeatherResult> GetAsync(double latitude, double longitude)
    {
        var url = FormattableString.Invariant($"v1/weather?lat={latitude}&lon={longitude}");

        var response = await http.GetFromJsonAsync<ApiNinjasResponse>(url)
            ?? throw new InvalidOperationException("Empty response from API Ninjas");

        return new WeatherResult(
            response.Temp, response.FeelsLike, response.Humidity,
            response.WindSpeed, response.CloudPct);
    }

    // Matches API Ninjas' JSON. Private, so nothing outside this class knows about it.
    private record ApiNinjasResponse(
        [property: JsonPropertyName("temp")] double Temp,
        [property: JsonPropertyName("feels_like")] double FeelsLike,
        [property: JsonPropertyName("humidity")] int Humidity,
        [property: JsonPropertyName("wind_speed")] double WindSpeed,
        [property: JsonPropertyName("cloud_pct")] int CloudPct);
}