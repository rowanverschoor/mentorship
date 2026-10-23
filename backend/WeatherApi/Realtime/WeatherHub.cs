using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WeatherApi.Realtime;

// Browsers connect here to receive results. No methods: only the server sends.
[Authorize]
public class WeatherHub : Hub;
