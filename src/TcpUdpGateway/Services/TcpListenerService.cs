using System.Net;
using System.Net.Sockets;
using System.Text;
using StackExchange.Redis;

namespace TcpUdpGateway.Services;

public sealed class TcpListenerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TcpListenerService> _logger;
    private readonly int _port;

    public TcpListenerService(
        IConnectionMultiplexer redis,
        ILogger<TcpListenerService> logger,
        IConfiguration config)
    {
        _redis = redis;
        _logger = logger;
        _port = int.Parse(config["TCP_PORT"] ?? "9000");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        _logger.LogInformation("TCP listener started on port {Port}", _port);

        while (!stoppingToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; 
            }

            _ = HandleClientAsync(client, stoppingToken);
        }

        listener.Stop();
        _logger.LogInformation("TCP listener stopped.");
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        EndPoint endpoint = client.Client.RemoteEndPoint;
        _logger.LogInformation("TCP client connected: {Endpoint}", endpoint);

        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var db = _redis.GetDatabase();

            string? line;
            while (!ct.IsCancellationRequested &&
                   (line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                await db.ListLeftPushAsync("sensor-data", line);

                _logger.LogDebug("TCP queued message from {Endpoint}: {Msg}", endpoint, line);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("TCP client {Endpoint} disconnected with error: {Error}",
                endpoint, ex.Message);
        }
        finally
        {
            client.Dispose();
            _logger.LogInformation("TCP client disconnected: {Endpoint}", endpoint);
        }
    }
}