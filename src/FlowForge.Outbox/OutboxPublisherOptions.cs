using FlowForge.Contracts;

namespace FlowForge.Outbox;

public sealed class OutboxPublisherOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = KafkaTopics.JobEvents;
}
