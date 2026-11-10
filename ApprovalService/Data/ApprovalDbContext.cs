using Microsoft.EntityFrameworkCore;

namespace ApprovalService.Data;

public class ApprovalDbContext(DbContextOptions<ApprovalDbContext> options) : DbContext(options)
{
    public DbSet<ApprovalDecision> Approvals => Set<ApprovalDecision>();
}

public class ApprovalDecision
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Region { get; set; } = "";
    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected
    public string? DecidedBy { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
