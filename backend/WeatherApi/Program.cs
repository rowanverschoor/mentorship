using Microsoft.AspNetCore.HttpOverrides;
using WeatherApi.Auth;
using WeatherApi.Resilience;
using WeatherApi.Services;
using System.Security.Claims;
using WeatherApi.Queue;


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
builder.Services.AddSingleton<IWeatherQueue, ChannelWeatherQueue>();
builder.Services.AddSingleton<WeatherResults>();
builder.Services.AddHostedService<WeatherWorker>();


var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPost("/weather", (double lat, double lon, ClaimsPrincipal user, IWeatherQueue queue, WeatherResults results) =>
{
    if (lat is < -90 or > 90 || lon is < -180 or > 180)
        return Results.BadRequest("Latitude must be -90..90 and longitude -180..180.");

    var job = new WeatherJob(Guid.NewGuid(), user.Identity!.Name!, lat, lon);

    // Mark as pending before enqueueing, so a fast worker can never be overwritten by "pending".
    results.Set(job.Id, new JobResult(job.User));
    if (queue.TryEnqueue(job))
        return Results.Accepted($"/weather/{job.Id}", new { id = job.Id });

    results.Remove(job.Id);
    return Results.Problem("Too many queued requests, try again later.", statusCode: 503);
}).RequireAuthorization().RequireRateLimiting(RateLimitExtensions.PolicyName);

// Only the user who queued the job can read it.
app.MapGet("/weather/{id:guid}", (Guid id, ClaimsPrincipal user, WeatherResults results) =>
    results.Get(id) is { } r && r.User == user.Identity!.Name
        ? Results.Ok(new { done = r.Done, weather = r.Weather, error = r.Error })
        : Results.NotFound()).RequireAuthorization();


app.MapLogin();

app.Run();
