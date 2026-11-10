using ApprovalService.Data;
using MassTransit;
using Shared.Contracts;

namespace ApprovalService.Consumers;

public class ApprovalRequestedConsumer(ApprovalDbContext db) : IConsumer<ApprovalRequested>
{
    public async Task Consume(ConsumeContext<ApprovalRequested> context)
    {
        var msg = context.Message;
        if (await db.Approvals.FindAsync(msg.ApprovalId) is not null)
            return;

        db.Approvals.Add(new ApprovalDecision
        {
            Id = msg.ApprovalId,
            RequestId = msg.RequestId,
            CustomerEmail = msg.CustomerEmail,
            Region = msg.Region,
            Status = "Pending",
            RequestedAt = msg.RequestedAt
        });
        await db.SaveChangesAsync();
    }
}
