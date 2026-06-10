namespace FlowForge.Contracts;

public static class KafkaTopics
{
    public const string JobEvents = "flowforge.job.events";
    public const string JobEventsDlq = "flowforge.job.events.dlq";
    public const string JobLogs = "flowforge.job.logs";
}
