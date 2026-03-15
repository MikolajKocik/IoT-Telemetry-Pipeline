using StackExchange.Redis;
using DataWorker.Workers;
using DataWorker.Workers.RabbitMq;

var builder = Host.CreateApplicationBuilder(args);

var redisCs = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
var redisCfg = ConfigurationOptions.Parse(redisCs);
redisCfg.AbortOnConnectFail = false;
redisCfg.ConnectRetry = 5;
redisCfg.ConnectTimeout = 5000;
redisCfg.ReconnectRetryPolicy = new ExponentialRetry(1000);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisCfg));

builder.Services.AddSingleton<OutboxStore>();
builder.Services.AddSingleton<RabbitMqPublisher>();

builder.Services.AddHostedService<TelemetryWorker>();
builder.Services.AddHostedService<OutboxRelay>();

var host = builder.Build();
host.Run();
