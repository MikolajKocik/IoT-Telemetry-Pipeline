using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace src.WsHub;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, Channel<string>> _connections = new();

    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public string Add()
    {
        string id = Guid.NewGuid().ToString();

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,   
            SingleWriter = false  
        });

        _connections[id] = channel;
        _logger.LogInformation("WS client connected: {Id}. Total: {Count}", id, _connections.Count);
        return id;
    }

    public void Remove(string id)
    {
        if (_connections.TryRemove(id, out var channel))
        {
            channel.Writer.Complete();
            _logger.LogInformation("WS client disconnected: {Id}. Total: {Count}", id, _connections.Count);
        }
    }

    public ChannelReader<string>? GetReader(string id) =>
        _connections.TryGetValue(id, out var ch) ? ch.Reader : null;

    public void Broadcast(string message)
    {
        foreach (var (id, channel) in _connections)
        {
            if (!channel.Writer.TryWrite(message))
            {
                _logger.LogDebug("Channel full for {Id}, dropping message", id);
            }
        }
    }

    public int Count => _connections.Count;
}