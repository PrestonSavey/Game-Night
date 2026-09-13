using GameNight.Api.Hubs;
using GameNight.Application;
using GameNight.Application.Games;
using GameNight.Application.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddGameNight();

// The transport supplies the runtime's way of talking back to clients.
builder.Services.AddSingleton<ISessionBroadcaster, SignalRBroadcaster>();

const string ClientCors = "client";
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options => options.AddPolicy(ClientCors, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // Required for the SignalR WebSocket handshake from a different origin.
    .AllowCredentials()));

var app = builder.Build();

app.UseCors(ClientCors);

app.MapHub<GameHub>("/hub/game");

// Container Apps probes this. Keep it cheap and dependency-free.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/modes", (IGameModeRegistry modes) => Results.Ok(modes.All));

app.Run();
