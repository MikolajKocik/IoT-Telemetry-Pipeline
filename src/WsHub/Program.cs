using System.Net.WebSockets;
using System.Text;
using StackExchange.Redis;
using src.WsHub;

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

builder.Services.AddSingleton<ConnectionManager>();

builder.Services.AddHostedService<RedisSubscriberService>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapGet("/healthz", (ConnectionManager cm) =>
    Results.Ok(new { status = "healthy", connections = cm.Count }));

app.MapGet("/ws/live", async (HttpContext context, ConnectionManager connections) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;

        await context.Response.WriteAsync(
            "This endpoint only accepts WebSocket connections. " +
            "Connect with ws://iot.local/ws/live");

        return;
    }

    var ws = await context.WebSockets.AcceptWebSocketAsync();
    var id = connections.Add();

    try
    {
          await Task.WhenAny(
            SendLoopAsync(ws, connections, id, context.RequestAborted),
            ReceiveLoopAsync(ws, context.RequestAborted)
        );
    }
    finally
    {
        connections.Remove(id);

        if (ws.State == WebSocketState.Open)
        {
            await ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Server closing",
                CancellationToken.None);
        }
        ws.Dispose();
    }
});

app.Run();

static async Task SendLoopAsync(
    WebSocket ws,
    ConnectionManager connections,
    string id,
    CancellationToken ct)
{
    var reader = connections.GetReader(id);
    if (reader is null) return;

    await foreach (var message in reader.ReadAllAsync(ct))
    {
        if (ws.State != WebSocketState.Open) break;

        var bytes = Encoding.UTF8.GetBytes(message);

        await ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: ct);
    }
}

static async Task ReceiveLoopAsync(WebSocket ws, CancellationToken ct)
{
    byte[] buffer = new byte[1024];

    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
    {
        WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await ws.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Closing",
                ct);
            break;
        }
    }
}