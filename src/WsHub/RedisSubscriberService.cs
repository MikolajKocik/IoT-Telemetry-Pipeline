using StackExchange.Redis;

namespace src.WsHub;

public sealed class RedisSubscriberService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ConnectionManager _connections;
    private readonly ILogger<RedisSubscriberService> _logger;

    public const string Channel = "sensor-live";

    public RedisSubscriberService(
        IConnectionMultiplexer redis,
        ConnectionManager connections,
        ILogger<RedisSubscriberService> logger)
    {
        _redis = redis;
        _connections = connections;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();

        await sub.SubscribeAsync(
            RedisChannel.Literal(Channel),
            (_, message) =>
            {
                if (message.IsNullOrEmpty) return;

                var text = (string)message!;
                _logger.LogDebug("Pub/Sub received, broadcasting to {Count} clients", _connections.Count);

                _connections.Broadcast(text);
            });

        _logger.LogInformation("Subscribed to Redis channel '{Channel}'", Channel);

         await Task.Delay(Timeout.Infinite, stoppingToken);

        await sub.UnsubscribeAsync(RedisChannel.Literal(Channel));
        _logger.LogInformation("Unsubscribed from Redis channel '{Channel}'", Channel);
    }
}