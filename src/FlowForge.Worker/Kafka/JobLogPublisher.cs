using Confluent.Kafka;
using FlowForge.Contracts;

namespace FlowForge.Worker.Kafka;

public sealed class JobLogPublisher(
    IProducer<string, string> producer,
    ILogger<JobLogPublisher> logger)
{
    public void Publish(
        Guid runId,
        int stepNo,
        string stepType,
        string level,
        string workerId,
        string message,
        int attempt,
        DateTimeOffset timestamp,
        DateTimeOffset? startedAt = null,
        string? error = null)
    {
        var value = WorkerStepLogFactory.CreateJson(
            runId,
            stepNo,
            stepType,
            level,
            workerId,
            message,
            attempt,
            timestamp,
            startedAt,
            error);

        try
        {
            producer.Produce(
                KafkaTopics.JobLogs,
                new Message<string, string>
                {
                    Key = runId.ToString(),
                    Value = value
                },
                report =>
                {
                    if (report.Error.IsError)
                    {
                        logger.LogWarning(
                            "Publishing job log failed for run {RunId}, step {StepNo}: {Reason}",
                            runId,
                            stepNo,
                            report.Error.Reason);
                    }
                });
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogWarning(
                ex,
                "Publishing job log failed synchronously for run {RunId}, step {StepNo}.",
                runId,
                stepNo);
        }
        catch (KafkaException ex)
        {
            logger.LogWarning(
                ex,
                "Publishing job log failed synchronously for run {RunId}, step {StepNo}.",
                runId,
                stepNo);
        }
    }
}
