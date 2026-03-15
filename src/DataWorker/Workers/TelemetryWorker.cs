using StackExchange.Redis;

namespace DataWorker.Workers;

public sealed class TelemetryWorker : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly OutboxStore _outbox;
    private readonly ILogger<TelemetryWorker> _logger;
    private readonly IConfiguration _config;

    private const string LiveChannel = "sensor-live";

    public TelemetryWorker(
        IConnectionMultiplexer redis,
        OutboxStore outbox,
        ILogger<TelemetryWorker> logger,
        IConfiguration config)
    {
        _redis = redis;
        _outbox = outbox;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int pollDelay = int.Parse(_config["WORKER_POLL_DELAY_MS"]    ?? "500");
        int processDelay = int.Parse(_config["WORKER_PROCESS_DELAY_MS"] ?? "2000");

        _logger.LogInformation("TelemetryWorker started.");

        var db  = _redis.GetDatabase();
        var pub = _redis.GetSubscriber();

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await db.ListRightPopAsync("sensor-data");

            if (!message.IsNull)
            {
                var payload = (string)message!;
                _logger.LogWarning("[TASK LOADED] {Payload}", payload);

                await Task.Delay(processDelay, stoppingToken);

                var eventId = Guid.NewGuid().ToString();
                _outbox.Insert(eventId, payload);
                _logger.LogInformation("[OUTBOX] Inserted event {Id}", eventId);

                await pub.PublishAsync(RedisChannel.Literal(LiveChannel), payload);
                _logger.LogInformation("[PUBSUB] Published to '{Ch}'", LiveChannel);
            }
            else
            {
                await Task.Delay(pollDelay, stoppingToken);
            }
        }

        _logger.LogInformation("TelemetryWorker shutting down.");
    }
}