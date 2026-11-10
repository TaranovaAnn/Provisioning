using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using WorkflowService.Data;

namespace WorkflowService.Services;

public class Orchestrator(WorkflowDbContext db, IPublishEndpoint bus, ILogger<Orchestrator> log)
{
    private static readonly string[] InfraSteps =
        ["ValidatePlan", "CreateAccount", "AllocateResource", "ConfigureNetwork", "EnableBilling"];

    public async Task StartAsync(RequestCreated msg)
    {
        var now = DateTimeOffset.UtcNow;
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            RequestId = msg.RequestId,
            CustomerEmail = msg.CustomerEmail,
            Plan = msg.Plan.ToString(),
            Region = msg.Region,
            CurrentStep = "ValidatePlan",
            Status = "Running",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Instances.Add(instance);
        await AddStepAsync(instance.Id, "ValidatePlan");
        await db.SaveChangesAsync();

        await bus.Publish(new RequestStatusChanged(msg.RequestId, "PlanValidated", now));

        if (msg.Plan == PlanType.Enterprise)
        {
            var approvalId = Guid.NewGuid();
            instance.CurrentStep = "AwaitingApproval";
            instance.PendingApprovalId = approvalId;
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await bus.Publish(new ApprovalRequested(
                approvalId, msg.RequestId, msg.CustomerEmail, msg.Region, DateTimeOffset.UtcNow));
            await bus.Publish(new RequestStatusChanged(msg.RequestId, "AwaitingApproval", DateTimeOffset.UtcNow));
            log.LogInformation("Workflow {Id} waiting for Enterprise approval", instance.Id);
            return;
        }

        await ContinueInfraAsync(instance);
    }

    public async Task OnApprovalAsync(ApprovalDecided msg)
    {
        var instance = await db.Instances
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.RequestId == msg.RequestId && x.Status == "Running");

        if (instance is null)
        {
            log.LogWarning("No running workflow for request {RequestId}", msg.RequestId);
            return;
        }

        await AddStepAsync(instance.Id, msg.Approved ? "ApprovalApproved" : "ApprovalRejected");

        if (!msg.Approved)
        {
            instance.Status = "Failed";
            instance.CurrentStep = "Rejected";
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await bus.Publish(new ProvisionFailed(
                msg.RequestId, instance.CustomerEmail, msg.Comment ?? "Approval rejected", msg.DecidedAt));
            return;
        }

        await ContinueInfraAsync(instance);
    }

    private async Task ContinueInfraAsync(WorkflowInstance instance)
    {
        foreach (var step in InfraSteps.Skip(1)) // ValidatePlan already done
        {
            instance.CurrentStep = step;
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            await AddStepAsync(instance.Id, step);
            await db.SaveChangesAsync();
            await bus.Publish(new RequestStatusChanged(instance.RequestId, step, DateTimeOffset.UtcNow));
            await Task.Delay(50); // учебная пауза между шагами
        }

        instance.Status = "Completed";
        instance.CurrentStep = "Completed";
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        await AddStepAsync(instance.Id, "Completed");
        await db.SaveChangesAsync();

        var plan = Enum.Parse<PlanType>(instance.Plan);
        await bus.Publish(new ProvisionCompleted(
            instance.RequestId, instance.CustomerEmail, plan, instance.Region, DateTimeOffset.UtcNow));

        log.LogInformation("Workflow {Id} completed for request {RequestId}", instance.Id, instance.RequestId);
    }

    private async Task AddStepAsync(Guid workflowId, string name)
    {
        db.Steps.Add(new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            StepName = name,
            Result = "Ok",
            CompletedAt = DateTimeOffset.UtcNow
        });
        await Task.CompletedTask;
    }
}
