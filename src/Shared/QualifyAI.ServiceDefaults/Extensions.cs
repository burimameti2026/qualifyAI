using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using QualifyAI.ServiceDefaults.Observability;
using Serilog;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var serviceName =
            Environment.GetEnvironmentVariable("SERVICE_NAME")
            ?? builder.Environment.ApplicationName;

        var seqUrl =
            Environment.GetEnvironmentVariable("SEQ_URL")
            ?? builder.Configuration["Seq:Url"]
            ?? "http://seq:5341";

        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("ServiceName", serviceName)
            .WriteTo.Console()
            .WriteTo.Seq(seqUrl));

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        var redisConnection = builder.Configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = $"{serviceName}:";
            });
        }

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseMiddleware<CorrelationIdMiddleware>();
        // Liveness must only report whether this process can serve requests.
        // Dependency failures belong in logs/readiness, not in Docker liveness.
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            service = Environment.GetEnvironmentVariable("SERVICE_NAME")
                ?? app.Environment.ApplicationName
        }));
        app.MapGet("/status", () => Results.Ok(new
        {
            service = Environment.GetEnvironmentVariable("SERVICE_NAME")
                ?? app.Environment.ApplicationName,
            environment = app.Environment.EnvironmentName,
            utc = DateTime.UtcNow
        }));
        return app;
    }
}
