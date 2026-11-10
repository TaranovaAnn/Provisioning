using Microsoft.EntityFrameworkCore;

namespace RequestService.Data;

public class RequestDbContext(DbContextOptions<RequestDbContext> options) : DbContext(options)
{
    public DbSet<ProvisionRequest> Requests => Set<ProvisionRequest>();
}

public class ProvisionRequest
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Plan { get; set; } = "Standard";
    public string Region { get; set; } = "";
    public string Status { get; set; } = "Created";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
