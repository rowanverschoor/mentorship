namespace WeatherApi.Models;

public record WeatherResult(
    double Temperature,
    double FeelsLike,
    int Humidity,
    double WindSpeed,
    int CloudPercentage);