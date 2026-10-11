using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.Homes.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;

namespace Senswave.DataSources.EndTests.Mqtt.Brokers;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class DeleteBroker(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotRemoveBrokerWhenWorking()
    {
        // Arrange
        var client = CreateClient();
        var connectionService = Services.GetRequiredService<IClientService>();

        // Act
        var id = await PostMqttBroker();
        await RemoveHomes(id);
        await StartBrokerClient(id);

        await AuthorizeClientAsUser(client);
        var firstConnectionResult = connectionService!.GetClient(id);
        var response = await client.DeleteAsync($"{Paths.BrokersPath}/{id}");

        var secondConnectionResult = connectionService!.GetClient(id);

        // Assert
        Assert.True(firstConnectionResult.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(secondConnectionResult.IsSuccess);

        await StopBrokerClientInternal(id);
    }

    private async Task RemoveHomes(Guid brokerId)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        var references = await context.DataSourceReferences
            .Where(x => x.DataSourceId == brokerId)
            .ToListAsync();

        context.RemoveRange(references);
        await context.SaveChangesAsync();

        scope.Dispose();
    }
}
