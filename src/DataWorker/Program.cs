using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var redisConnectionString = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddHostedService<TelemetryWorker>();

var host = builder.Build();
host.Run();

public class TelemetryWorker : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TelemetryWorker> _logger;
    
    public TelemetryWorker(IConnectionMultiplexer redis, ILogger<TelemetryWorker> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ready to work");
    
        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await db.ListRightPopAsync("sensor-data");

            if (!message.IsNull)
            {
                _logger.LogWarning($"[TASK LOADED] Analyze data from sensor: {message}");

                await Task.Delay(2000, stoppingToken);

                _logger.LogInformation("[TASK ENDED] Waiting for next ...\n");
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}
