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

        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("sim.events");

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
                    _logger.LogDebug("Received message: {Message}", message);

                    var eventData = JsonSerializer.Deserialize<SimulationTickEvent>(message);
                    
                    if (eventData?.EventType == "sim.time.tick")
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var routeService = scope.ServiceProvider.GetRequiredService<IRouteService>();
                        
                        await routeService.ProcessSimulationTickAsync(
                            eventData.EventId,
                            eventData.TickMinutes);

                        consumer.Commit(consumeResult);
                        _logger.LogDebug("Processed tick event {EventId}", eventData.EventId);
                    }
                    else
                    {
                        // Not a tick event, just commit
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from Kafka");
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