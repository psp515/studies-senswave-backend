using MailKit;
using MassTransit.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Senswave.Abstractions.Transport;
using Senswave.DataSources.Api;
using Senswave.Infrastructure;
using Senswave.Infrastructure.Diagnostics.OpenTelemetry;
using Senswave.Infrastructure.Grpc;
using Senswave.Infrastructure.Web.Diagnostics;
using Senswave.Infrastructure.Web.Diagnostics.Health;
using Senswave.LiveUpdates.Api;
using Senswave.Users.Api;

namespace Senswave.Api.Diagnostics;

public static class DiagnosticsExtensions
{
    public const string TransportAttributeName = "senswave.transport";

    public static IServiceCollection AddDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealth(configuration);
        services.AddScoped<DiagnosticsMiddleware>();

        var options = services.AddOpenTelemetryBase(configuration);

        if (!options.Enabled)
            return services;

        var transport = TransportOptions.FromConfiguration(configuration);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(
            [
                new KeyValuePair<string, object>(TransportAttributeName, transport.Mode.ToString())
            ]))
            .WithTracing(x => x
                .SetSampler(new AlwaysOnSampler())
                .AddSource(InfrastructureModule.DefaultListenerName)
                .AddSource(DataSourcesModule.DefaultListenerName)
                .AddSource(LiveUpdatesModule.DefaultListenerName)
                .AddSource(UsersModule.DefaultListenerName)
                .AddSource(DiagnosticHeaders.DefaultListenerName)
                .AddSource(GrpcInfrastructureExtensions.DefaultListenerName)
                .AddSource(Telemetry.SmtpClient.ActivitySourceName)
                .AddAspNetCoreInstrumentation(x =>
                {
                    x.Filter = httpContext =>
                    {
                        if (!httpContext.Request.Path.HasValue)
                            return true;

                        if (httpContext.Request.Path.Value.Contains("swagger"))
                            return false;

                        return true;
                    };

                    x.EnrichWithHttpResponse = (activity, response) =>
                    {
                        if (activity.GetTagItem("http.route") is string route &&
                            activity.DisplayName == response.HttpContext.Request.Method.ToUpperInvariant())
                        {
                            activity.DisplayName = $"{response.HttpContext.Request.Method.ToUpperInvariant()} {route}";
                        }
                    };
                })
                .AddHttpClientInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter()
            ).WithMetrics(x => x
                .AddNpgsqlInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter(Telemetry.SmtpClient.MeterName)
                .AddOtlpExporter()
            );

        return services;
    }
}
