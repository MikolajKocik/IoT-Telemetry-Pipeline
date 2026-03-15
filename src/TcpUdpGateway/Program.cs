using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

string redisConnectionString = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
redisConfig.AbortOnConnectFail = false;
redisConfig.ConnectRetry = 5;
redisConfig.ConnectTimeout = 5000;
redisConfig.ReconnectRetryPolicy = new ExponentialRetry(1000);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConfig)
);

// for sensors that need acknowledgement
builder.Services.AddHostedService<TcpListenerService>();

// for high-frequency sensors
builder.Services.AddHostedService<UdpListenerService>();

var host = builder.Build();
host.Run();