using Confluent.Kafka;
using GroundControl.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace GroundControl.Infrastructure.Kafka;

public class SimulationTickConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SimulationTickConsumer> _logger;
    private readonly string _bootstrapServers;
    private readonly string _groupId;

    public SimulationTickConsumer(
        IServiceProvider serviceProvider,
        ILogger<SimulationTickConsumer> logger,
        string bootstrapServers = "kafka:9092",
        string groupId = "ground-service")
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _bootstrapServers = bootstrapServers;
        _groupId = groupId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Kafka consumer for sim.events topic");

        // Delay start to allow HTTP server to initialize
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 10000,
            HeartbeatIntervalMs = 3000
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        try
        {
            consumer.Subscribe("sim.events");
            _logger.LogInformation("Subscribed to sim.events topic");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe to Kafka topic, will retry");
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult == null)
                        continue;

                    var message = consumeResult.Message.Value;
                    _logger.LogInformation("Received Kafka message: {Message}", message);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var eventData = JsonSerializer.Deserialize<SimulationTickEvent>(message, options);

                    if (eventData?.EventType == "sim.time.tick")
                    {
                        _logger.LogInformation("Processing tick event {EventId}, tickMinutes={TickMinutes}",
                            eventData.EventId, eventData.TickMinutes);

                        using var scope = _serviceProvider.CreateScope();
                        var routeService = scope.ServiceProvider.GetRequiredService<IRouteService>();

                        await routeService.ProcessSimulationTickAsync(
                            eventData.EventId,
                            eventData.TickMinutes);

                        consumer.Commit(consumeResult);
                        _logger.LogInformation("Successfully processed tick event {EventId}", eventData.EventId);
                    }
                    else
                    {
                        _logger.LogWarning("Received non-tick event, type={EventType}", eventData?.EventType);
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    // Ignore topic not found errors during startup
                    if (!ex.Message.Contains("Unknown topic"))
                    {
                        _logger.LogError(ex, "Error consuming message from Kafka");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing simulation tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer cancelled");
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Kafka consumer closed");
        }
    }
}

public class SimulationTickEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int TickMinutes { get; set; }
    public DateTime SimulationTime { get; set; }
}