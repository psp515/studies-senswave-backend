using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Senswave.Abstractions.Transport;
using Senswave.Api;
using Senswave.Infrastructure.Persistence;
using Senswave.TestInfrastructure.Extensions;
using Senswave.TestInfrastructure.Fixtures.Database;
using Senswave.TestInfrastructure.Fixtures.MessageBus;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Initializers;
using Senswave.Users.Application.Auth.Google.Services;
using Senswave.Users.Domain.Interfaces;

namespace Senswave.TestInfrastructure.TestEnvironments.Grpc;

/// <summary>
/// Host running with <c>Transport:Mode=Grpc</c> against its own database, so gRPC collections never interfere with REST ones.
/// </summary>
public class GrpcTestEnvironment : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly static SemaphoreSlim _synchronizer = new(1, 1);

    protected virtual TransportMode Mode => TransportMode.Grpc;

#if TESTS
    private static readonly IDatabase GrpcDatabase = new PostgreSqlFixture("SenswaveEndTestsGrpc");
    private static readonly IMessageBus GrpcMessageBus = new RabbitMqFixture();
#else
    private static readonly IDatabase GrpcDatabase = new PostgreSqlContainerFixture("SenswaveEndTestsGrpc");
    private static readonly IMessageBus GrpcMessageBus = new RabbitMqContainerFixture();
#endif

    /// <summary>Each derived mode overrides these with its own fixtures, so modes never share a database.</summary>
    protected virtual IDatabase Database => GrpcDatabase;

    protected virtual IMessageBus MessageBus => GrpcMessageBus;

    #region ServicesMocks

    public Mock<IGoogleService> GoogleServiceMock { get; } = new();

    public Mock<IEmailService> EmailServiceMock { get; } = new();

    #endregion

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TransportOptions.SectionName}:{nameof(TransportOptions.Mode)}"] = Mode.ToString()
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.ReplaceRabbitMqBus(MessageBus);
            services.ReplacePostgreSql(Database);

            services.ReplaceOptions(BaseTestEnvironment.AnonymousRateLimiterOptions);
            services.ReplaceOptions(BaseTestEnvironment.UserRateLimiterOptions);

            services.ReplaceOptions(BaseTestEnvironment.BrokerOptions);
            services.ReplaceOptions(BaseTestEnvironment.HomeOptions);
            services.ReplaceOptions(BaseTestEnvironment.DevicesOptions);
            services.ReplaceOptions(BaseTestEnvironment.AutomationOptions);

            services.ReplaceService(GoogleServiceMock.Object);
            services.ReplaceService(EmailServiceMock.Object);
        });

        builder.UseEnvironment("Test");
    }

    public virtual async Task InitializeAsync()
    {
        await _synchronizer.WaitAsync();

        try
        {
            await Database.InitializeAsync();
            await MessageBus.InitializeAsync();

            if (!Database.IsWorking())
            {
                //If running locally start postgres container or change running to release
                throw new InvalidOperationException("PostgreSQL container is not running.");
            }

            if (!MessageBus.IsWorking())
            {
                //If running locally start rabbitmq container or change running to release
                throw new InvalidOperationException("RqbbitMq container is not running.");
            }

            using var scope = Services.CreateScope();

            var databaseContextInitializer = scope.ServiceProvider.GetRequiredService<DatabaseContextInitializer>();
            await databaseContextInitializer.Initialize(CancellationToken.None);

            var userInitializer = new TestUsersInitializer(scope);
            await userInitializer.InitializeUsers();
        }
        finally
        {
            _synchronizer.Release();
        }
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}
