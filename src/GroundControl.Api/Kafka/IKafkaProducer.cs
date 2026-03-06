namespace GroundControl.Api.Kafka;

public interface IKafkaProducer
{
    Task PublishAsync(string topic, string key, object payload, CancellationToken ct = default);
}
