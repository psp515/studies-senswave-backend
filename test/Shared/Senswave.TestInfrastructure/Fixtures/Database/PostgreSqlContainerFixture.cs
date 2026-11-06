using DotNet.Testcontainers.Containers;
using System.Runtime.InteropServices;
using Testcontainers.PostgreSql;

namespace Senswave.TestInfrastructure.Fixtures.Database;

public class PostgreSqlContainerFixture : IDatabase
{
    private const string UnixSocketAddr = "unix:/var/run/docker.sock";

    public PostgreSqlContainer Container { get; }

    private static string Image => "postgres:18.6";
    private static string Username => "testuser";
    private static string Password => "testuser";

    private readonly string database;

    public string Hostname => Container.Hostname;

    public PostgreSqlContainerFixture(string databaseName)
    {
        database = databaseName;

        var builder = new PostgreSqlBuilder(Image)
                    .WithPortBinding(5432, true)
                    .WithDatabase(databaseName)
                    .WithUsername(Username)
                    .WithPassword(Password)
                    .WithCleanUp(true);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            builder.WithDockerEndpoint(UnixSocketAddr);

        Container = builder.Build();
    }

    public int GetPort() => Container.GetMappedPublicPort(5432);
    public string GetHostname() => Container.Hostname;
    public string GetUsername() => Username;
    public string GetPassword() => Password;
    public string GetDatabase() => database;
    public bool IsWorking() => Container.State == TestcontainersStates.Running;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.StopAsync();
    }
}
