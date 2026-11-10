using MassTransit;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Prometheus;
using Shared.Observability;
using WorkflowService.Consumers;
using WorkflowService.Data;
using WorkflowService.Services;

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
    var metrics = builder.Services.AddServiceMetrics("workflow");
    builder.Services.AddDbContext<WorkflowDbContext>(opt =>
    {
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5433;Database=workflow_db;Username=lab2;Password=lab2");
        opt.AddInterceptors(new EfCoreMetricsInterceptor(metrics));
    });
    builder.Services.AddScoped<Orchestrator>();

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<RequestCreatedConsumer>();
        x.AddConsumer<ApprovalDecidedConsumer>();
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
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    app.UseServiceHttpMetrics();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Workflow Service v1"));

    app.MapMetrics();
    app.MapGet("/health", () => Results.Ok(new { service = "workflow-service", status = "ok" }))
        .WithTags("Health");

    app.MapGet("/api/workflows", async (WorkflowDbContext db) =>
        Results.Ok(await db.Instances.OrderByDescending(x => x.CreatedAt).ToListAsync()))
        .WithTags("Workflows");

    app.MapGet("/api/workflows/by-request/{requestId:guid}", async (Guid requestId, WorkflowDbContext db) =>
    {
        var instance = await db.Instances
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.RequestId == requestId);
        if (instance is null) return Results.NotFound();

        var steps = await db.Steps
            .Where(s => s.WorkflowId == instance.Id)
            .OrderBy(s => s.CompletedAt)
            .ToListAsync();

        return Results.Ok(new { instance, steps });
    }).WithTags("Workflows");

    logger.Info("WorkflowService starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "WorkflowService stopped unexpectedly");
    throw;
}
finally
{
    LogManager.Shutdown();
}
