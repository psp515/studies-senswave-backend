using Asp.Versioning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Senswave.Abstractions.Modules;
using Senswave.Abstractions.Transport;
using Senswave.Api.Cors;
using Senswave.Api.Diagnostics;
using Senswave.Api.Public;
using Senswave.Api.RateLimiters;
using Senswave.Api.RateLimiters.Anonymous;
using Senswave.Api.RateLimiters.User;
using Senswave.Automations.Api;
using Senswave.DataSources.Api;
using Senswave.Devices.Api;
using Senswave.Homes.Api;
using Senswave.Infrastructure;
using Senswave.Infrastructure.Grpc;
using Senswave.Infrastructure.Grpc.Modules;
using Senswave.Infrastructure.Module;
using Senswave.Infrastructure.Persistence;
using Senswave.Infrastructure.Web;
using Senswave.Infrastructure.Web.Diagnostics;
using Senswave.Infrastructure.Web.Diagnostics.Health;
using Senswave.Infrastructure.Web.Endpoints;
using Senswave.Infrastructure.Web.Exceptions;
using Senswave.LiveUpdates.Api;
using Senswave.Users.Api;
using Senswave.Users.Api.Grpc;
using Senswave.Users.Infrastructure;
using Senswave.Users.Infrastructure.Middlewares;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace Senswave.Api;

public class Startup
{
    private const string SenswaveNetWebsitePolicy = "SenswaveWebsitePolicy";

    private readonly IList<Assembly> _senswaveAssemblies;
    private readonly IList<ISenswaveModule> _modules;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly TransportOptions _transport;

    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _senswaveAssemblies = ModuleLoader.LoadAssemblies();
        _modules = ModuleLoader.LoadModules(_senswaveAssemblies);
        _configuration = configuration;
        _environment = environment;
        _transport = TransportOptions.FromConfiguration(configuration);

        if (_modules.Count == 0)
            throw new InvalidDataException("Failed to load modules");
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"));
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSignalRSwaggerGen(ssgOptions => ssgOptions.ScanAssemblies(_senswaveAssemblies.Where(x => x.FullName?.Contains("LiveUpdates") ?? false)));

            options.CustomSchemaIds(x => x.FullName);

            options.SwaggerDoc(AutomationsModule.GroupName, new OpenApiInfo
            {
                Title = "Automations REST API",
                Version =AutomationsModule.GroupName
            });

            options.SwaggerDoc(DataSourcesModule.GroupName, new OpenApiInfo
            {
                Title = "Data Sources REST API",
                Version = DataSourcesModule.GroupName
            });

            options.SwaggerDoc(DevicesModule.GroupName, new OpenApiInfo
            {
                Title = "Devices REST API",
                Version = DevicesModule.GroupName
            });

            options.SwaggerDoc(HomesModule.GroupName, new OpenApiInfo
            {
                Title = "Homes REST API",
                Version = HomesModule.GroupName
            });

            options.SwaggerDoc(UsersModule.GroupName, new OpenApiInfo
            {
                Title = "User REST API",
                Version = UsersModule.GroupName
            });

            options.SwaggerDoc(LiveUpdatesModule.GroupName, new OpenApiInfo
            {
                Title = "Live Update REST API",
                Version = LiveUpdatesModule.GroupName
            });

            options.SwaggerDoc(DiagnosticModule.GroupName, new OpenApiInfo
            {
                Title = "Diagnostics API",
                Version = DiagnosticModule.GroupName
            });

            options.SwaggerDoc(PublicModule.GroupName, new OpenApiInfo
            {
                Title = "Public API",
                Version = PublicModule.GroupName
            });


            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description =
                     "JWT Authorization header using the Bearer scheme. \r\n\r\n " +
                     "Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\n" +
                     "Example: \"Bearer 12345abcdef\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });

            options.DocInclusionPredicate((docName, apiDesc) =>
            {
                if (!apiDesc.TryGetMethodInfo(out var methodInfo))
                    return false;

                var tags = apiDesc.ActionDescriptor.EndpointMetadata
                    .OfType<TagsAttribute>()
                    .SelectMany(attr => attr.Tags)
                    .ToList();

                if (tags.Count==0)
                    return false;

                var prefixByDocument = new Dictionary<string, string>
                {
                    { AutomationsModule.GroupName, AutomationsModule.ModuleName },
                    { HomesModule.GroupName, HomesModule.ModuleName },
                    { DevicesModule.GroupName, DevicesModule.ModuleName },
                    { DataSourcesModule.GroupName, DataSourcesModule.ModuleName },
                    { UsersModule.GroupName, UsersModule.ModuleName },
                    { LiveUpdatesModule.GroupName, LiveUpdatesModule.ModuleName },
                    { PublicModule.GroupName, PublicModule.ModuleName }
                };

                if (!prefixByDocument.TryGetValue(docName, out var requiredPrefix))
                    return false;

                return tags.Any(tag => tag.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase));
            });

            options.DocumentFilter<HealthEndpointDocumentFilter>();
        });

        services.AddInfrastructure(_configuration);
        services.AddWebInfrastructure();

        services.AddDiagnostics(_configuration);
        services.AddRateLimiters(_configuration);
        services.AddPublicModule(_configuration);

        services.Configure<TransportOptions>(options => options.Mode = _transport.Mode);

        if (_transport.IsGrpc)
        {
            services.AddGrpcInfrastructure(options =>
            {
                options.EnableDetailedErrors = _environment.IsDevelopment();

                // The only consent check in gRPC modes; LegalMiddleware is REST-only.
                options.Interceptors.Add<LegalConsentInterceptor>();
            });
        }
        else
        {
            services.AddScoped<LegalMiddleware>();
        }

        services.AddExceptionHandler<GlobalExceptionHandlerMiddleware>();
        services.AddTransient<DatabaseContextInitializer>();

        services.AddDataProtection()
            .PersistKeysToDbContext<UsersContext>();

        services.AddCors(_configuration);

        foreach (var module in _modules)
        {
            module.Register(services, _configuration);
        }
    }

    public void Configure(
        IApplicationBuilder app,
        IEnumerable<IEndpoint> minimalApiEndpoints,
        IOptions<RateLimitersOptions> rateLimitingOptions,
        IOptions<HealthOptions> healthOptions,
        ILogger<Startup> logger)
    {
        logger.LogInformation("Modules: {modules}", string.Join(", ", _modules.Select(x => x.Name)));
        logger.LogInformation("Transport: {transport}", _transport.Mode);

        app.UseMiddleware<DiagnosticsMiddleware>();

        if (_environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint($"/swagger/{AutomationsModule.GroupName}/swagger.json", $"{AutomationsModule.ModuleName} REST API");
                options.SwaggerEndpoint($"/swagger/{DataSourcesModule.GroupName}/swagger.json", $"{DataSourcesModule.ModuleName} REST API");
                options.SwaggerEndpoint($"/swagger/{DevicesModule.GroupName}/swagger.json", $"{DevicesModule.ModuleName} REST API");
                options.SwaggerEndpoint($"/swagger/{HomesModule.GroupName}/swagger.json", $"{HomesModule.ModuleName} REST API");
                options.SwaggerEndpoint($"/swagger/{UsersModule.GroupName}/swagger.json", $"{UsersModule.ModuleName} REST API");
                options.SwaggerEndpoint($"/swagger/{LiveUpdatesModule.GroupName}/swagger.json", $"{LiveUpdatesModule.ModuleName} REST API");
                options.SwaggerEndpoint($"/swagger/{DiagnosticModule.GroupName}/swagger.json", "Diagnostics REST API");
                options.SwaggerEndpoint($"/swagger/{PublicModule.GroupName}/swagger.json", "Public REST API");
            });
        }
        else
        {
            app.UseExceptionHandler();
        }

        app.UseHttpsRedirection();

        app.UseCors(SenswaveNetWebsitePolicy);

        app.UseRouting();

        if (rateLimitingOptions.Value.Enabled)
            app.UseRateLimiter();

        app.UseMiddleware<HealthCheckMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        if (!_transport.IsGrpc)
            app.UseMiddleware<LegalMiddleware>();

        app.UseEndpoints(endpoints =>
        {
            var apiVersionSet = endpoints.NewApiVersionSet()
                .HasApiVersion(new ApiVersion(1))
                .HasApiVersion(new ApiVersion(2))
                .ReportApiVersions()
                .Build();

            var defaultGroutBuilder = endpoints
                .MapGroup("/api/v{version:apiVersion}")
                .WithApiVersionSet(apiVersionSet)
                .RequireRateLimiting(UserRateLimiter.PolicyName);

            var publicNotVersionedGroupBuilder = endpoints
                .MapGroup("")
                .RequireRateLimiting(AnonymousRateLimiter.PolicyName);

            publicNotVersionedGroupBuilder.MapApiPublicEndpoints();

            if (_transport.IsGrpc)
            {
                // Browser navigation targets (e.g. the email confirmation link) stay HTTP in every mode.
                defaultGroutBuilder.MapNavigationEndpoints(minimalApiEndpoints);

                endpoints.MapGrpcModules(_modules);

                return;
            }

            defaultGroutBuilder.MapEndpoints(minimalApiEndpoints);

            var publicVersionedGroupBuilder = endpoints
                .MapGroup("/api/v{version:apiVersion}")
                .WithApiVersionSet(apiVersionSet)
                .RequireRateLimiting(AnonymousRateLimiter.PolicyName);

            publicVersionedGroupBuilder.MapPublicEndpoints(minimalApiEndpoints);

            endpoints.UseLiveUpdates();

            // Separte Rate Limiter
            endpoints.UseAuthEndpoints(AnonymousRateLimiter.PolicyName);
        });
    }
}

