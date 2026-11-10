using MassTransit;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using NotificationService.Consumers;
using NotificationService.Data;
using Prometheus;
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
    var metrics = builder.Services.AddServiceMetrics("notification");
    builder.Services.AddDbContext<NotificationDbContext>(opt =>
    {
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5433;Database=notification_db;Username=lab2;Password=lab2");
        opt.AddInterceptors(new EfCoreMetricsInterceptor(metrics));
    });

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<NotifyOnProvisionCompletedConsumer>();
        x.AddConsumer<NotifyOnProvisionFailedConsumer>();
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
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    app.UseServiceHttpMetrics();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service v1"));

    app.MapMetrics();
    app.MapGet("/health", () => Results.Ok(new { service = "notification-service", status = "ok" }))
        .WithTags("Health");

    app.MapGet("/api/notifications", async (NotificationDbContext db) =>
        Results.Ok(await db.Notifications.OrderByDescending(x => x.SentAt).ToListAsync()))
        .WithTags("Notifications");

    logger.Info("NotificationService starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "NotificationService stopped unexpectedly");
    throw;
}
finally
{
    LogManager.Shutdown();
}
