using Microsoft.EntityFrameworkCore;

namespace NotificationService.Data;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationLog> Notifications => Set<NotificationLog>();
}

public class NotificationLog
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Channel { get; set; } = "Email";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Sent";
    public DateTimeOffset SentAt { get; set; }
}
