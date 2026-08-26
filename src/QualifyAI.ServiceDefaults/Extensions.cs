using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using QualifyAI.ServiceDefaults.Consul;
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

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        builder.Services.AddHealthChecks();
        builder.Services.AddHostedService<ConsulRegistrationHostedService>();

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.MapHealthChecks("/health");
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
