using Senswave.TestInfrastructure.TestEnvironments.Limit;
using System.Net;

namespace Senswave.Homes.EndTests.Limits.Rooms;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostRooms(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        var client = CreateClient();
        var limit = LimitTestEnvironment.HomeOptions.Limits.RoomsPerHome;

        // Act
        await CleanHomes();
        var home = await PostHome(76, 51);
        await AuthorizeClientAsUser(client);

        for (int i = 0; i < limit; i++)
        {
            await PostRoom(home);
        }

        var response = await PostRoomNoChecks(home);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
