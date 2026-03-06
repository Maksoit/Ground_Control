namespace GroundControl.Api.Kafka;

/// <summary>
/// No-op Kafka producer used when Kafka is disabled or not configured.
/// </summary>
public class NullKafkaProducer : IKafkaProducer
{
    public Task PublishAsync(string topic, string key, object payload, CancellationToken ct = default)
        => Task.CompletedTask;
}
