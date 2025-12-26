// Controller: realtime distribution of measurements to connected clients (SignalR).
// Consumes Kafka measurements and fans-out to subscribers; Redis is used for scale-out backplane.

using Controller.Hubs;
using Controller.Services;

var builder = WebApplication.CreateBuilder(args);

var redis = builder.Configuration["Redis:ConnectionString"]
            ?? builder.Configuration["Redis__ConnectionString"]
            ?? "localhost:6379";

// Redis backplane makes SignalR broadcasts work across multiple Controller pods.
builder.Services
    .AddSignalR()
    .AddStackExchangeRedis(redis);

builder.Services.AddHostedService<KafkaMeasurementConsumer>();

builder.Services.AddHealthChecks();

// Simple in-memory CRUD resource (PRPO requires GET/POST/PUT/DELETE on each service).
// This represents client-side filtering / subscription preferences.
var subscriptions = new Dictionary<string, string>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "controller", status = "running" }));

app.MapGet("/subscriptions", () => Results.Ok(subscriptions.Select(kv => new { clientId = kv.Key, filter = kv.Value })));

app.MapPost("/subscriptions/{clientId}", (string clientId, string filter) =>
{
    subscriptions[clientId] = filter;
    return Results.Created($"/subscriptions/{clientId}", new { clientId, filter });
});

app.MapPut("/subscriptions/{clientId}", (string clientId, string filter) =>
{
    if (!subscriptions.ContainsKey(clientId)) return Results.NotFound();
    subscriptions[clientId] = filter;
    return Results.Ok(new { clientId, filter });
});

app.MapDelete("/subscriptions/{clientId}", (string clientId) =>
{
    if (!subscriptions.Remove(clientId)) return Results.NotFound();
    return Results.NoContent();
});

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapHub<MeasurementsHub>("/hub/measurements");

app.Run();
