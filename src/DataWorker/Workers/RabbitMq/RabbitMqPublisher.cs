using RabbitMQ.Client;
using System.Text;

namespace DataWorker.Workers.RabbitMq;

public sealed class RabbitMqPublisher : IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;

    private readonly string _host;
    private readonly string _exchange;
    private readonly string _queue;
    private readonly string _routingKey;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public const string ExchangeName = "sensor-events";
    public const string QueueName = "processed-telemetry";
    public const string RoutingKeyVal = "telemetry.processed";

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _host = config["RABBITMQ_HOST"] ?? "localhost";
        _exchange = ExchangeName;
        _queue = QueueName;
        _routingKey = RoutingKeyVal;
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _host,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _logger.LogInformation("Connecting to RabbitMQ at {Host}…", _host);
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(
            exchange: _exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        await _channel.QueueDeclareAsync(
            queue: _queue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: _queue,
            exchange: _exchange,
            routingKey: _routingKey);

        _logger.LogInformation(
            "RabbitMQ ready. Exchange='{Ex}' Queue='{Q}' RoutingKey='{RK}'",
            _exchange, _queue, _routingKey);
    }

    public async Task PublishAsync(string messageId, string payload)
    {
        if (_channel is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");

        var props = new BasicProperties
        {
            Persistent = true,      
            MessageId = messageId,    
            ContentType = "application/json",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        byte[] body = Encoding.UTF8.GetBytes(payload);

        await _channel.BasicPublishAsync(
            exchange: _exchange,
            routingKey: _routingKey,
            mandatory: false,
            basicProperties: props,
            body: body);

        _logger.LogDebug("Published to RabbitMQ: MessageId={Id}", messageId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}