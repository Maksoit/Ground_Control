using System.Text.Json;
using Confluent.Kafka;
using GroundControl.Api.Models;
using GroundControl.Api.Services;

namespace GroundControl.Api.Workers;

/// <summary>
/// Background worker that listens to the sim.time.tick Kafka topic
/// and triggers TTL expiry logic on each tick.
/// </summary>
public class SimTimeTickWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SimTimeTickWorker> _logger;

    public SimTimeTickWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SimTimeTickWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogInformation("Kafka not configured, SimTimeTickWorker will not start.");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "ground-sim-tick",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("sim.time.tick");
        _logger.LogInformation("SimTimeTickWorker subscribed to sim.time.tick");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result == null)
                        continue;

                    var tick = JsonSerializer.Deserialize<SimTimeTick>(result.Message.Value,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (tick == null)
                        continue;

                    using var scope = _scopeFactory.CreateScope();
                    var routeService = scope.ServiceProvider.GetRequiredService<IRouteService>();
                    await routeService.ExpireByTickAsync(tick.TickMinutes, tick.EventId, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "Kafka consume error");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unexpected error in SimTimeTickWorker");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
