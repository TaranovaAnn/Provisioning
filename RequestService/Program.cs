using MassTransit;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Prometheus;
using RequestService.Consumers;
using RequestService.Data;
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
    var metrics = builder.Services.AddServiceMetrics("request");
    builder.Services.AddDbContext<RequestDbContext>(opt =>
    {
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5433;Database=request_db;Username=lab2;Password=lab2");
        opt.AddInterceptors(new EfCoreMetricsInterceptor(metrics));
    });

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<RequestStatusChangedConsumer>();
        x.AddConsumer<RequestProvisionCompletedConsumer>();
        x.AddConsumer<RequestProvisionFailedConsumer>();
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
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    app.UseServiceHttpMetrics();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Request Service v1"));

    app.MapMetrics();
    app.MapGet("/health", () => Results.Ok(new { service = "request-service", status = "ok" }))
        .WithTags("Health");

    app.MapPost("/api/requests", async (CreateRequestDto dto, RequestDbContext db, IPublishEndpoint bus) =>
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerEmail) || string.IsNullOrWhiteSpace(dto.Region))
            return Results.BadRequest(new { error = "CustomerEmail and Region are required" });

        var plan = dto.Plan?.Equals("Enterprise", StringComparison.OrdinalIgnoreCase) == true
            ? PlanType.Enterprise
            : PlanType.Standard;

        var now = DateTimeOffset.UtcNow;
        var entity = new ProvisionRequest
        {
            Id = Guid.NewGuid(),
            CustomerEmail = dto.CustomerEmail.Trim(),
            Plan = plan.ToString(),
            Region = dto.Region.Trim(),
            Status = "Created",
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Requests.Add(entity);
        await db.SaveChangesAsync();

        await bus.Publish(new RequestCreated(entity.Id, entity.CustomerEmail, plan, entity.Region, now));
        logger.Info("Request created {RequestId} plan={Plan}", entity.Id, entity.Plan);

        return Results.Created($"/api/requests/{entity.Id}", entity);
    }).WithTags("Requests");

    app.MapGet("/api/requests/{id:guid}", async (Guid id, RequestDbContext db) =>
    {
        var entity = await db.Requests.FindAsync(id);
        return entity is null ? Results.NotFound() : Results.Ok(entity);
    }).WithTags("Requests");

    app.MapGet("/api/requests", async (RequestDbContext db) =>
        Results.Ok(await db.Requests.OrderByDescending(x => x.CreatedAt).ToListAsync()))
        .WithTags("Requests");

    logger.Info("RequestService starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "RequestService stopped unexpectedly");
    throw;
}
finally
{
    LogManager.Shutdown();
}

public record CreateRequestDto(string CustomerEmail, string Plan, string Region);
