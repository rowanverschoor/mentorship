var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.UseHttpsRedirection();

app.MapGet("/weather", async (double lat, double lon, HttpClient http, IConfiguration config) =>
{
    var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"https://api.api-ninjas.com/v1/weather?lat={lat}&lon={lon}");
    request.Headers.Add("X-Api-Key", config["ApiNinjas:Key"]);

    var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();

    return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
});

app.Run();
