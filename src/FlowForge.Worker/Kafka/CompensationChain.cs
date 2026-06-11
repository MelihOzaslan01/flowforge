using FlowForge.Contracts;

namespace FlowForge.Worker.Kafka;

public static class CompensationChain
{
    public static EventEnvelope CreateAfterStepFailed(StepFailed failed, DateTimeOffset? occurredAt = null)
    {
        if (failed.StepNo <= 1)
        {
            return EventEnvelope.From(
                new JobRunFailed(failed.RunId, failed.StepNo, failed.Error),
                occurredAt: occurredAt);
        }

        return EventEnvelope.From(
            new CompensateStep(failed.RunId, failed.StepNo - 1, failed.StepNo, failed.Steps),
            occurredAt: occurredAt);
    }

    public static EventEnvelope CreateAfterCompensateStep(CompensateStep compensate, DateTimeOffset? occurredAt = null)
    {
        return EventEnvelope.From(
            new StepCompensated(compensate.RunId, compensate.StepNo, compensate.FailedStep, compensate.Steps),
            occurredAt: occurredAt);
    }

    public static EventEnvelope CreateAfterStepCompensated(StepCompensated compensated, DateTimeOffset? occurredAt = null)
    {
        if (compensated.StepNo > 1)
        {
            return EventEnvelope.From(
                new CompensateStep(
                    compensated.RunId,
                    compensated.StepNo - 1,
                    compensated.FailedStep,
                    compensated.Steps),
                occurredAt: occurredAt);
        }

        return EventEnvelope.From(
            new JobRunFailed(
                compensated.RunId,
                compensated.FailedStep,
                $"Step {compensated.FailedStep} failed."),
            occurredAt: occurredAt);
    }
}
