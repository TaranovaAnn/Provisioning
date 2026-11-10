using MassTransit;
using NotificationService.Data;
using Shared.Contracts;

namespace NotificationService.Consumers;

public class NotifyOnProvisionCompletedConsumer(NotificationDbContext db, ILogger<NotifyOnProvisionCompletedConsumer> log)
    : IConsumer<ProvisionCompleted>
{
    public async Task Consume(ConsumeContext<ProvisionCompleted> context)
    {
        var msg = context.Message;
        var entry = new NotificationLog
        {
            Id = Guid.NewGuid(),
            RequestId = msg.RequestId,
            CustomerEmail = msg.CustomerEmail,
            Channel = "Email",
            Subject = "Провижининг завершён",
            Body = $"Заявка {msg.RequestId} ({msg.Plan}, {msg.Region}) успешно выполнена.",
            Status = "Sent",
            SentAt = DateTimeOffset.UtcNow
        };
        db.Notifications.Add(entry);
        await db.SaveChangesAsync();
        log.LogInformation("Notified {Email} about completed request {RequestId}", msg.CustomerEmail, msg.RequestId);
    }
}

public class NotifyOnProvisionFailedConsumer(NotificationDbContext db, ILogger<NotifyOnProvisionFailedConsumer> log)
    : IConsumer<ProvisionFailed>
{
    public async Task Consume(ConsumeContext<ProvisionFailed> context)
    {
        var msg = context.Message;
        var entry = new NotificationLog
        {
            Id = Guid.NewGuid(),
            RequestId = msg.RequestId,
            CustomerEmail = msg.CustomerEmail,
            Channel = "Email",
            Subject = "Провижининг не выполнен",
            Body = $"Заявка {msg.RequestId}: {msg.Reason}",
            Status = "Sent",
            SentAt = DateTimeOffset.UtcNow
        };
        db.Notifications.Add(entry);
        await db.SaveChangesAsync();
        log.LogInformation("Notified {Email} about failed request {RequestId}", msg.CustomerEmail, msg.RequestId);
    }
}
