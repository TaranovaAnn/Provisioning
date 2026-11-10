using ApprovalService.Consumers;
using ApprovalService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Prometheus;
using Shared.Contracts;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("config/appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Host.UseNLog();

var nlogPath = Path.Combine(builder.Environment.ContentRootPath, "config", "nlog.config");
LogManager.Setup().LoadConfigurationFromFile(nlogPath);
LogManager.Configuration!.Variables["logDir"] = Path.Combine(builder.Environment.ContentRootPath, "logs");
var logger = LogManager.GetCurrentClassLogger();

try
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    var metrics = builder.Services.AddServiceMetrics("approval");
    builder.Services.AddDbContext<ApprovalDbContext>(opt =>
    {
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5433;Database=approval_db;Username=lab2;Password=lab2");
        opt.AddInterceptors(new EfCoreMetricsInterceptor(metrics));
    });

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<ApprovalRequestedConsumer>();
        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
            });
            cfg.ConfigureEndpoints(ctx);
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApprovalDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    app.UseServiceHttpMetrics();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Approval Service v1"));

    app.MapMetrics();
    app.MapGet("/health", () => Results.Ok(new { service = "approval-service", status = "ok" }))
        .WithTags("Health");

    app.MapGet("/api/approvals", async (ApprovalDbContext db) =>
        Results.Ok(await db.Approvals.OrderByDescending(x => x.RequestedAt).ToListAsync()))
        .WithTags("Approvals");

    app.MapGet("/api/approvals/pending", async (ApprovalDbContext db) =>
        Results.Ok(await db.Approvals.Where(x => x.Status == "Pending").ToListAsync()))
        .WithTags("Approvals");

    app.MapPost("/api/approvals/{id:guid}/decide", async (
        Guid id, DecideDto dto, ApprovalDbContext db, IPublishEndpoint bus) =>
    {
        var entity = await db.Approvals.FindAsync(id);
        if (entity is null) return Results.NotFound();
        if (entity.Status != "Pending")
            return Results.Conflict(new { error = "Already decided", status = entity.Status });

        var approved = dto.Approved;
        entity.Status = approved ? "Approved" : "Rejected";
        entity.DecidedBy = string.IsNullOrWhiteSpace(dto.DecidedBy) ? "approver" : dto.DecidedBy.Trim();
        entity.Comment = dto.Comment;
        entity.DecidedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await bus.Publish(new ApprovalDecided(
            entity.Id, entity.RequestId, approved, entity.DecidedBy, entity.Comment, entity.DecidedAt.Value));

        logger.Info("Approval {ApprovalId} decided approved={Approved}", entity.Id, approved);
        return Results.Ok(entity);
    }).WithTags("Approvals");

    logger.Info("ApprovalService starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "ApprovalService stopped unexpectedly");
    throw;
}
finally
{
    LogManager.Shutdown();
}

public record DecideDto(bool Approved, string? DecidedBy, string? Comment);
