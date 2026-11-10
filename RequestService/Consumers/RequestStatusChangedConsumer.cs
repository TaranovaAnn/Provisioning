using MassTransit;
using Microsoft.EntityFrameworkCore;
using RequestService.Data;
using Shared.Contracts;

namespace RequestService.Consumers;

public class RequestStatusChangedConsumer(RequestDbContext db) : IConsumer<RequestStatusChanged>
{
    public async Task Consume(ConsumeContext<RequestStatusChanged> context)
    {
        var msg = context.Message;
        var request = await db.Requests.FirstOrDefaultAsync(x => x.Id == msg.RequestId);
        if (request is null) return;
        if (request.Status is "Completed" or "Failed") return;

        request.Status = msg.Status;
        request.UpdatedAt = msg.ChangedAt;
        await db.SaveChangesAsync();
    }
}

public class RequestProvisionCompletedConsumer(RequestDbContext db) : IConsumer<ProvisionCompleted>
{
    public async Task Consume(ConsumeContext<ProvisionCompleted> context)
    {
        var request = await db.Requests.FirstOrDefaultAsync(x => x.Id == context.Message.RequestId);
        if (request is null) return;
        request.Status = "Completed";
        request.UpdatedAt = context.Message.CompletedAt;
        await db.SaveChangesAsync();
    }
}

public class RequestProvisionFailedConsumer(RequestDbContext db) : IConsumer<ProvisionFailed>
{
    public async Task Consume(ConsumeContext<ProvisionFailed> context)
    {
        var request = await db.Requests.FirstOrDefaultAsync(x => x.Id == context.Message.RequestId);
        if (request is null) return;
        request.Status = "Failed";
        request.UpdatedAt = context.Message.FailedAt;
        await db.SaveChangesAsync();
    }
}
