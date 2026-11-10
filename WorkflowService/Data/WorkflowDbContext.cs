using Microsoft.EntityFrameworkCore;

namespace WorkflowService.Data;

public class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowInstance> Instances => Set<WorkflowInstance>();
    public DbSet<WorkflowStep> Steps => Set<WorkflowStep>();
}

public class WorkflowInstance
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Plan { get; set; } = "Standard";
    public string Region { get; set; } = "";
    public string CurrentStep { get; set; } = "Created";
    public string Status { get; set; } = "Running";
    public Guid? PendingApprovalId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class WorkflowStep
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string StepName { get; set; } = "";
    public string Result { get; set; } = "Ok";
    public DateTimeOffset CompletedAt { get; set; }
}
