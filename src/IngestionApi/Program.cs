using System.Text.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

string redisConnectionString = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";

var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
redisConfig.AbortOnConnectFail = false;  
redisConfig.ConnectRetry = 5;
redisConfig.ConnectTimeout = 5000;
redisConfig.ReconnectRetryPolicy = new ExponentialRetry(1000);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConfig)
);

var app = builder.Build();

app.MapGet("/healthz", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();
        await db.PingAsync();

        return Results.Ok(new 
        { 
            status = "healthy", 
            redis = "connected" 
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Redis unreachable: {ex.Message}");
    }
});

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