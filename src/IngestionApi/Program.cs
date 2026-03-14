using System.Text.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redisConnectionString = builder.Configuration["REDIS_CONNECTION"]
    ?? "localhost:6379";
var multiprexer = ConnectionMultiplexer.Connect(redisConnectionString);

builder.Services.AddSingleton<IConnectionMultiplexer>(multiprexer);

var app = builder.Build();

app.MapPost("/api/telemetry", async (TelemetryData data, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();

    var message = JsonSerializer.Serialize(data);

    await db.ListLeftPushAsync("sensor-data", message);

    return Results.Accepted(value: new
    {
        status = "Queued",
        sensorId = data.SensorId
    });
});

app.Run();

public record TelemetryData(string SensorId, double Temperature, DateTime Timestamp);
