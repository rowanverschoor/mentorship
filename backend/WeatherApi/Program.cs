using WeatherApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IWeatherService, ApiNinjasWeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.api-ninjas.com/");
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["ApiNinjas:Key"]);
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"]!)
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.MapGet("/weather", async (double lat, double lon, IWeatherService weather) =>
    Results.Ok(await weather.GetAsync(lat, lon)));

app.Run();
