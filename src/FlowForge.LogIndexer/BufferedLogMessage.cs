using Confluent.Kafka;

namespace FlowForge.LogIndexer;

public sealed record BufferedLogMessage(
    ConsumeResult<string, string> Result,
    string IndexName,
    string DocumentId,
    string Json);
