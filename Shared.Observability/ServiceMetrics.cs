using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Shared.Observability;

/// <summary>
/// Краткие метрики: {service}_{metric} и {service}_db_{metric}.
/// Пример: request_http, request_cpu, request_mem, request_db_read, request_db_write.
/// </summary>
public sealed class ServiceMetrics
{
    public string Service { get; }
    public Gauge Health { get; }
    public Counter Http { get; }
    public Gauge Cpu { get; }
    public Gauge Mem { get; }
    public Counter DbRead { get; }
    public Counter DbWrite { get; }

    public ServiceMetrics(string service)
    {
        Service = service;
        Health = Metrics.CreateGauge($"{service}_health", $"Health of {service} (1=up)");
        Http = Metrics.CreateCounter($"{service}_http", $"HTTP requests handled by {service}");
        Cpu = Metrics.CreateGauge($"{service}_cpu", $"CPU % of {service} process");
        Mem = Metrics.CreateGauge($"{service}_mem", $"Working set bytes of {service}");
        DbRead = Metrics.CreateCounter($"{service}_db_read", $"DB reads by {service}");
        DbWrite = Metrics.CreateCounter($"{service}_db_write", $"DB writes by {service}");
        Health.Set(1);
    }
}

public sealed class ProcessMetricsHostedService(ServiceMetrics metrics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var process = Process.GetCurrentProcess();
        var lastCpu = process.TotalProcessorTime;
        var lastAt = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            process.Refresh();
            var now = DateTime.UtcNow;
            var cpu = process.TotalProcessorTime;
            var elapsed = (now - lastAt).TotalSeconds;
            if (elapsed > 0)
            {
                var pct = (cpu - lastCpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100.0;
                metrics.Cpu.Set(Math.Clamp(pct, 0, 100));
            }

            metrics.Mem.Set(process.WorkingSet64);
            lastCpu = cpu;
            lastAt = now;
        }
    }
}

public static class ObservabilityExtensions
{
    /// <summary>Регистрирует краткие метрики сервиса. Вернёт экземпляр для EF interceptor.</summary>
    public static ServiceMetrics AddServiceMetrics(this IServiceCollection services, string serviceShortName)
    {
        Metrics.SuppressDefaultMetrics();
        var metrics = new ServiceMetrics(serviceShortName);
        services.AddSingleton(metrics);
        services.AddHostedService<ProcessMetricsHostedService>();
        return metrics;
    }

    public static IApplicationBuilder UseServiceHttpMetrics(this IApplicationBuilder app)
    {
        var metrics = app.ApplicationServices.GetRequiredService<ServiceMetrics>();
        app.Use(async (ctx, next) =>
        {
            await next();
            if (!ctx.Request.Path.StartsWithSegments("/metrics"))
                metrics.Http.Inc();
        });
        return app;
    }
}
