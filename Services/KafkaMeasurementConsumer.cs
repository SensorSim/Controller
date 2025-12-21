using System.Text.Json;
using Confluent.Kafka;
using Controller.Dtos;
using Controller.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Controller.Services;

public sealed class KafkaMeasurementConsumer : BackgroundService
{
    private readonly ILogger<KafkaMeasurementConsumer> _logger;
    private readonly IHubContext<MeasurementsHub> _hub;
    private readonly IConfiguration _configuration;
    private readonly string _topic;
    private readonly string _bootstrapServers;

    public KafkaMeasurementConsumer(
        ILogger<KafkaMeasurementConsumer> logger,
        IHubContext<MeasurementsHub> hub,
        IConfiguration configuration)
    {
        _logger = logger;
        _hub = hub;
        _configuration = configuration;

        _topic = configuration["Kafka:Topic"]
                ?? configuration["Kafka__Topic"]
                ?? "measurements";

        _bootstrapServers = configuration["Kafka:BootstrapServers"]
                            ?? configuration["Kafka__BootstrapServers"]
                            ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var hostname = Environment.GetEnvironmentVariable("HOSTNAME")
                   ?? Environment.MachineName
                   ?? Guid.NewGuid().ToString("N");

    var groupId = $"controller-{hostname}";

    var config = new ConsumerConfig
    {
        BootstrapServers = _bootstrapServers,
        GroupId = groupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        SessionTimeoutMs = 10000
    };

    using var consumer = new ConsumerBuilder<string, string>(config).Build();

    _logger.LogInformation(
        "Controller Kafka consumer starting. bootstrap={bootstrap} topic={topic} group={group}",
        _bootstrapServers, _topic, groupId);

    TimeSpan backoff = TimeSpan.FromSeconds(2);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            consumer.Subscribe(_topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                var measurement = JsonSerializer.Deserialize<MeasurementOut>(result.Message.Value);
                if (measurement is null)
                    continue;

                await _hub.Clients.All.SendAsync(
                    "measurement",
                    measurement,
                    stoppingToken);

                backoff = TimeSpan.FromSeconds(2); // reset on success
            }
        }
        catch (ConsumeException ex) when (
            ex.Error.Code == ErrorCode.UnknownTopicOrPart ||
            ex.Error.IsError)
        {
            _logger.LogWarning(
                ex,
                "Kafka config topic not ready. Retrying in {delay}s",
                backoff.TotalSeconds);
        }


        catch (KafkaException ex)
        {
            _logger.LogWarning(
                ex,
                "Kafka error (controller). Retrying in {delay}s",
                backoff.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected controller Kafka failure");
        }

        await Task.Delay(backoff, stoppingToken);
        backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
    }

    try { consumer.Close(); } catch { }
}

}
