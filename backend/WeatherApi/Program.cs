using Polly.CircuitBreaker;
using Polly.Timeout;
using WeatherApi.Resilience;
using WeatherApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IWeatherService, ApiNinjasWeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.api-ninjas.com/");
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["ApiNinjas:Key"]);
})
.AddWeatherResilience();

builder.Services.AddWeatherRateLimiting();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"]!)
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();

app.MapGet("/weather", async (double lat, double lon, IWeatherService weather) =>
{
    if (lat is < -90 or > 90 || lon is < -180 or > 180)
        return Results.BadRequest("Latitude must be -90..90 and longitude -180..180.");

    try
    {
        return Results.Ok(await weather.GetAsync(lat, lon));
    }
    catch (BrokenCircuitException)
    {
        // Circuit is open: the provider is failing, we are not even trying.
        return Results.Problem("Weather provider is temporarily unavailable.", statusCode: 503);
    }
    catch (Exception e) when (e is HttpRequestException or TimeoutRejectedException)
    {
        // Retries are used up and the provider still failed.
        return Results.Problem("Weather provider failed.", statusCode: 502);
    }
}).RequireRateLimiting(RateLimitExtensions.PolicyName);

app.Run();
