using MassTransit.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Senswave.DataSources.Domain.Diagnostics;
using Senswave.Infrastructure;
using Senswave.Infrastructure.Diagnostics.OpenTelemetry;
using Senswave.Infrastructure.Web.Diagnostics.Health;

namespace Senswave.Presentation.DataSource.Worker.Diagnostics;

public static class DiagnosticsExtensions
{
    public static IServiceCollection AddDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealth(configuration);

        var options = services.AddOpenTelemetryBase(configuration);

        if (!options.Enabled)
            return services;

        services.AddOpenTelemetry()
            .WithTracing(x => x
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSamplingRatio)))
                .AddSource(InfrastructureModule.DefaultListenerName)
                .AddSource(DataSourcesActivityProvider.DefaultListenerName)
                .AddSource(DiagnosticHeaders.DefaultListenerName)
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter()
            ).WithMetrics(x => x
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter((_, reader) =>
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricExportIntervalSeconds * 1000)
            );

        return services;
    }
}
