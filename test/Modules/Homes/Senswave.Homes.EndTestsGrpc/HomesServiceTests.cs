using Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class HomesServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateHome → GetHome → GetHomes → UpdateHome → GetCurrentHome → SetHomeDataSource → DeleteHomeDataSource → DeleteHome.
    /// </summary>
    [Fact]
    public async Task HomeLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new HomesService.HomesServiceClient(channel);

        // CreateHome
        var created = await client.CreateHomeAsync(new CreateHomeRequest { Name = "My home", Icon = "home", Latitude = 51, Longitude = 17 });
        var homeId = created.Id;

        // GetHome + GetHomes
        var home = await client.GetHomeAsync(new GetHomeRequest { HomeId = homeId });
        var homes = await client.GetHomesAsync(new GetHomesRequest { Page = 1, Size = 100 });

        Assert.Equal("My home", home.Name);
        Assert.True(home.IsOwner);
        Assert.Equal(51, home.Location.Latitude);
        Assert.Contains(homes.Items, x => x.Id == homeId);

        // UpdateHome
        await client.UpdateHomeAsync(new UpdateHomeRequest { HomeId = homeId, Name = "Renamed home", Icon = "house" });
        var updated = await client.GetHomeAsync(new GetHomeRequest { HomeId = homeId });

        Assert.Equal("Renamed home", updated.Name);

        // GetCurrentHome
        var current = await client.GetCurrentHomeAsync(new GetCurrentHomeRequest { Latitude = 51, Longitude = 17 });

        Assert.True(Guid.TryParse(current.Id, out _));

        // SetHomeDataSource + DeleteHomeDataSource
        var brokerId = await channel.CreateBrokerAsync();
        await client.SetHomeDataSourceAsync(new SetHomeDataSourceRequest { HomeId = homeId, BrokerId = brokerId });
        var withDataSource = await client.GetHomeAsync(new GetHomeRequest { HomeId = homeId });

        Assert.Equal(brokerId, withDataSource.DataSource.Id);

        await client.DeleteHomeDataSourceAsync(new DeleteHomeDataSourceRequest { HomeId = homeId });

        // DeleteHome
        await client.DeleteHomeAsync(new DeleteHomeRequest { HomeId = homeId });
    }
}
