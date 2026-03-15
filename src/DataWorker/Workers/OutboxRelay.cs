using DataWorker.Workers.RabbitMq;

namespace DataWorker.Workers;

public sealed class OutboxRelay : BackgroundService
{
    private readonly OutboxStore _outbox;
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<OutboxRelay> _logger;
    private readonly int _pollMs;

    public OutboxRelay(
        OutboxStore outbox,
        RabbitMqPublisher publisher,
        ILogger<OutboxRelay> logger,
        IConfiguration config)
    {
        _outbox = outbox;
        _publisher = publisher;
        _logger = logger;
        _pollMs = int.Parse(config["OUTBOX_POLL_DELAY_MS"] ?? "1000");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);

        _logger.LogInformation("OutboxRelay started. Polling every {Ms}ms", _pollMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var unsent = _outbox.GetUnsent();

                foreach (var entry in unsent)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        await _publisher.PublishAsync(entry.Id, entry.Payload);

                        _outbox.MarkSent(entry.Id);

                        _logger.LogInformation(
                            "Outbox relayed: {Id} | {Payload}", entry.Id, entry.Payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to relay outbox entry {Id}, will retry", entry.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxRelay poll cycle failed");
            }

            await Task.Delay(_pollMs, stoppingToken);
        }

        _logger.LogInformation("OutboxRelay shutting down.");
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        int delay = 2000;
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await _publisher.ConnectAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "RabbitMQ connect attempt {A}/5 failed: {Err}. Retrying in {D}ms…",
                    attempt, ex.Message, delay);

                if (attempt == 5) throw;

                await Task.Delay(delay, ct);
                delay *= 2; 
            }
        }
    }
}