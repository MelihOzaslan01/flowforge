using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.Worker.Kafka;

namespace FlowForge.UnitTests;

public sealed class CompensationChainTests
{
    [Fact]
    public void Failed_step_three_produces_reverse_compensation_chain()
    {
        var runId = Guid.NewGuid();
        var steps = new[]
        {
            CreateStep(1, "ExtractData"),
            CreateStep(2, "TransformData"),
            CreateStep(3, "GenerateReport")
        };

        var failed = new StepFailed(runId, 3, "boom", 4, steps);
        var events = new List<EventEnvelope>();

        var next = CompensationChain.CreateAfterStepFailed(failed);
        events.Add(next);

        while (next.EventType != nameof(JobRunFailed))
        {
            next = next.EventType switch
            {
                nameof(CompensateStep) => CompensationChain.CreateAfterCompensateStep(
                    next.DeserializePayload<CompensateStep>()),
                nameof(StepCompensated) => CompensationChain.CreateAfterStepCompensated(
                    next.DeserializePayload<StepCompensated>()),
                _ => throw new InvalidOperationException($"Unexpected event {next.EventType}.")
            };

            events.Add(next);
        }

        Assert.Equal(
            [
                nameof(CompensateStep),
                nameof(StepCompensated),
                nameof(CompensateStep),
                nameof(StepCompensated),
                nameof(JobRunFailed)
            ],
            events.Select(message => message.EventType));

        Assert.Equal(2, events[0].DeserializePayload<CompensateStep>().StepNo);
        Assert.Equal(2, events[1].DeserializePayload<StepCompensated>().StepNo);
        Assert.Equal(1, events[2].DeserializePayload<CompensateStep>().StepNo);
        Assert.Equal(1, events[3].DeserializePayload<StepCompensated>().StepNo);

        var terminal = events[^1].DeserializePayload<JobRunFailed>();
        Assert.Equal(runId, terminal.RunId);
        Assert.Equal(3, terminal.FailedStep);
    }

    private static JobStepDefinition CreateStep(int stepNo, string stepType)
    {
        return new JobStepDefinition(
            Guid.NewGuid(),
            stepNo,
            stepType,
            JsonSerializer.SerializeToElement(new { }, ContractJson.Options),
            3);
    }
}
