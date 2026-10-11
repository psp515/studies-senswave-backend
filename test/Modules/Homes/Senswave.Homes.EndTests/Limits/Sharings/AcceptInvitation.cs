using Senswave.TestInfrastructure.TestEnvironments.Limit;
using System.Net;

namespace Senswave.Homes.EndTests.Limits.Sharings;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class AcceptInvitation(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        await CleanHomes();
        var home = await PostHomeWithBroker();
        await PrepareHomeSharingForAdmin(home);
        var secondFriend = CreateClient();

        // Act
        var passwordResponse = await InviteFriend(home, "user1@gmail.com", _displayPrivilege);
        await AuthorizeClientAsUser1(secondFriend);
        var response = await AcceptInvitationNoCheck(secondFriend, passwordResponse.Password);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
