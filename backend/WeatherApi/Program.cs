using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using WeatherApi.Auth;
using WeatherApi.Queue;
using WeatherApi.Realtime;
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

builder.Services.AddSignalR();
builder.Services.AddSingleton<IWeatherNotifier, SignalRWeatherNotifier>();
builder.Services.AddSingleton<IWeatherQueue, ChannelWeatherQueue>();
builder.Services.AddHostedService<WeatherWorker>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPost("/weather", (double lat, double lon, ClaimsPrincipal user, IWeatherQueue queue) =>
{
    if (lat is < -90 or > 90 || lon is < -180 or > 180)
        return Results.BadRequest("Latitude must be -90..90 and longitude -180..180.");

    var job = new WeatherJob(Guid.NewGuid(), user.Identity!.Name!, lat, lon);

    // The result comes later, through the hub. The id lets the browser match it to this request.
    return queue.TryEnqueue(job)
        ? Results.Accepted(value: new { id = job.Id })
        : Results.Problem("Too many queued requests, try again later.", statusCode: 503);
}).RequireAuthorization().RequireRateLimiting(RateLimitExtensions.PolicyName);

app.MapHub<WeatherHub>("/hubs/weather");
app.MapLogin();

app.Run();
