using System.Text.Json;
using Confluent.Kafka;

namespace GroundControl.Api.Kafka;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private bool _disposed;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "kafka:9092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            MessageTimeoutMs = 5000,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(string topic, string key, object payload, CancellationToken ct = default)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = key,
                Value = JsonSerializer.Serialize(payload),
            };

            await _producer.ProduceAsync(topic, message, ct);
            _logger.LogDebug("Published to {Topic} key={Key}", topic, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish to Kafka topic {Topic}. Continuing without Kafka.", topic);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
            _disposed = true;
        }
    }
}
