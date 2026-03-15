using System.Net;
using System.Net.Sockets;
using System.Text;
using StackExchange.Redis;

namespace TcpUdpGateway.Services;

public sealed class UdpListenerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<UdpListenerService> _logger;
    private readonly int _port;

    public UdpListenerService(
        IConnectionMultiplexer redis,
        ILogger<UdpListenerService> logger,
        IConfiguration config)
    {
        _redis = redis;
        _logger = logger;
        _port = int.Parse(config["UDP_PORT"] ?? "9001");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(_port);
        _logger.LogInformation("UDP listener started on port {Port}", _port);

        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var message = Encoding.UTF8.GetString(result.Buffer);

            if (string.IsNullOrWhiteSpace(message)) continue;

            await db.ListLeftPushAsync("sensor-data", message);

            _logger.LogDebug("UDP queued from {Sender}: {Msg}",
                result.RemoteEndPoint, message);
        }

        _logger.LogInformation("UDP listener stopped.");
    }
}