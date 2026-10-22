using Microsoft.AspNetCore.HttpOverrides;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WeatherApi.Auth;
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

// Behind nginx every request comes from nginx's IP; trust X-Forwarded-For so the
// rate limiter sees the real client. Safe because only nginx can reach this API.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddJwtAuth(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
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
}).RequireAuthorization().RequireRateLimiting(RateLimitExtensions.PolicyName);

app.MapLogin();

app.Run();
