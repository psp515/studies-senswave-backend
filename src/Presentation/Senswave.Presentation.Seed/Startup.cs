using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using Refit;
using Senswave.DataSources.Api.Brokers.Clients.StartClient;
using Senswave.DataSources.Infrastructure;
using Senswave.Infrastructure.Persistence;
using Senswave.Presentation.Seed.Automations.Clients;
using Senswave.Presentation.Seed.Automations.Interfaces;
using Senswave.Presentation.Seed.Automations.Services;
using Senswave.Presentation.Seed.DataSources.Clients;
using Senswave.Presentation.Seed.Devices.Clients;
using Senswave.Presentation.Seed.Devices.Interfaces;
using Senswave.Presentation.Seed.Devices.Services;
using Senswave.Presentation.Seed.Homes.Clients;
using Senswave.Presentation.Seed.Seed.Options;
using Senswave.Presentation.Seed.Seed.Services;
using Senswave.Presentation.Seed.Users.Clients;
using Senswave.Users.Domain.Entity;
using Senswave.Users.Infrastructure;

namespace Senswave.Presentation.Seed;

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        var seedOptions = new SeedOptions();
        configuration.GetSection(SeedOptions.SectionName).Bind(seedOptions);

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddSingleton<IValidateOptions<SeedOptions>, SeedOptionsValidator>();
        services.AddOptions<SeedOptions>().ValidateOnStart();
        services.Configure<PostgreSqlOptions>(configuration.GetSection(PostgreSqlOptions.SectionName));

        services.AddRefitClient<IIdentityClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IUserClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IBrokerClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IClientsClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IHomeClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IRoomClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IDeviceClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IDashboardClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IWidgetClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IOperationClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddRefitClient<IAutomationClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(seedOptions.Instance));

        services.AddDbContext<UsersContext>();
        services.AddDbContext<DataSourcesContext>();

        services.AddIdentity<User, IdentityRole<Guid>>()
             .AddEntityFrameworkStores<UsersContext>()
             .AddApiEndpoints()
             .AddDefaultTokenProviders();

        services.AddTransient<IDeviceSeedingService, DeviceSeedingService>();
        services.AddTransient<IAutomationSeedingService, AutomationSeedingService>();
        services.AddHostedService<InitialSeedBackgroundService>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/startDefaultUserClient", async (
                DataSourcesContext context,
                IIdentityClient identityClient,
                IBrokerClient brokerClient,
                IClientsClient clientService,
                IOptions<SeedOptions> options) =>
            {
                try
                {
                    if (options.Value.DefaultUser is null)
                    {
                        return Results.BadRequest("Default user not found.");
                    }

                    var loginRequest = new LoginRequest
                    {
                        Email = options.Value.DefaultUser.Email,
                        Password = options.Value.DefaultUser.Password
                    };

                    var login = await identityClient.Login(loginRequest);

                    var brokers = await brokerClient.GetBrokers(login.AccessToken);

                    if (!brokers.Items.Any())
                    {
                        return Results.BadRequest("No brokers found.");
                    }

                    var brokerId = brokers.Items.First(x => x.Name == options.Value.DefaultUser.Broker.Name).Id;

                    var startClient = new StartClientDto
                    {
                        Password = options.Value.DefaultUser.Broker.Password,
                        Username = options.Value.DefaultUser.Broker.Username
                    };

                    await clientService.StartClient(login.AccessToken, brokerId, startClient);

                    return Results.Ok();
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });
        });
    }
}
