using Senswave.Infrastructure.Grpc.Errors;

namespace Senswave.Api.Cors;

public static class CorsExtensions
{
    public static IServiceCollection AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SenswaveWebsiteCorsOptions>(configuration.GetSection(SenswaveWebsiteCorsOptions.SectionName));

        var options = configuration.GetSection(SenswaveWebsiteCorsOptions.SectionName).Get<SenswaveWebsiteCorsOptions>();

        if (options is not null && !options.Enabled)
        {
            return services;
        }

        if (options is not null && options.AllowedOrigins.Length == 0)
        {
            throw new InvalidOperationException($"Configuration section '{SenswaveWebsiteCorsOptions.SectionName}' is missing or invalid.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy("SenswaveWebsitePolicy", builder =>
            {
                var corsOptions = configuration.GetSection(SenswaveWebsiteCorsOptions.SectionName).Get<SenswaveWebsiteCorsOptions>();

                builder.WithOrigins(corsOptions!.AllowedOrigins)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       // Browsers hide response headers and trailers from scripts unless exposed; gRPC-Web clients
                       // need the gRPC status and the Senswave error/trace trailers. Harmless for REST.
                       .WithExposedHeaders(
                           "grpc-status",
                           "grpc-message",
                           "grpc-encoding",
                           "grpc-accept-encoding",
                           GrpcErrorMetadata.ErrorsKey,
                           GrpcErrorMetadata.TraceIdKey);
            });
        });

        return services;
    }
}
