using System.Text.Json;
using Confluent.Kafka;
using Controller.Dtos;
using Controller.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Controller.Services;

public sealed class KafkaToSignalRWorker : BackgroundService
{
    private readonly ILogger<KafkaToSignalRWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<MeasurementsHub> _hub;

    public KafkaToSignalRWorker(
        ILogger<KafkaToSignalRWorker> logger,
        IConfiguration configuration,
        IHubContext<MeasurementsHub> hub)
    {
        _logger = logger;
        _configuration = configuration;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"]
                               ?? _configuration["Kafka__BootstrapServers"]
                               ?? "localhost:9092";

        var topic = _configuration["Kafka:Topic"]
                    ?? _configuration["Kafka__Topic"]
                    ?? "measurements";

        // Each Controller instance MUST have its own consumer group so every replica gets ALL messages.
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
        var groupId = $"controller-{hostname}";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {reason}", e.Reason))
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation("Controller consuming Kafka topic {topic} on {bootstrap} as group {group}", topic, bootstrapServers, groupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result?.Message?.Value is null)
                    continue;

                MeasurementIn? measurement;
                try
                {
                    measurement = JsonSerializer.Deserialize<MeasurementIn>(result.Message.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid message payload (skipping)");
                    continue;
                }

                if (measurement is null)
                    continue;

                // Broadcast to all connected clients.
                await _hub.Clients.All.SendAsync("measurement", measurement, stoppingToken);
            }
        }
        finally
        {
            try
            {
                consumer.Close();
            }
            catch
            {
                // ignore
            }
        }
    }
}
