using MassTransit;
using Shared.Contracts;
using WorkflowService.Services;

namespace WorkflowService.Consumers;

public class RequestCreatedConsumer(Orchestrator orchestrator) : IConsumer<RequestCreated>
{
    public Task Consume(ConsumeContext<RequestCreated> context) =>
        orchestrator.StartAsync(context.Message);
}

public class ApprovalDecidedConsumer(Orchestrator orchestrator) : IConsumer<ApprovalDecided>
{
    public Task Consume(ConsumeContext<ApprovalDecided> context) =>
        orchestrator.OnApprovalAsync(context.Message);
}
