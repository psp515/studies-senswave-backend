using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Senswave.Homes.Domain;
using Senswave.Homes.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class AcceptInvitationTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var passwordResponse = await InviteFriend(home, "admin@gmail.com", "Display");

        var invitation = RequestFactory.CreateAcceptInvitation(passwordResponse.Password);

        // Act
        var response = await user.PutAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CanAcceptInvitation()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var passwordResponse = await InviteFriend(home, "admin@gmail.com", "Display");

        var invitation = RequestFactory.CreateAcceptInvitation(passwordResponse.Password);

        // Act
        await AuthorizeClientAsAdmin(user);
        var response = await user.PutAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        await InviteFriend(home, "admin@gmail.com", "Display");

        var invitation = RequestFactory.CreateAcceptInvitation(null);

        // Act
        await AuthorizeClientAsAdmin(user);
        var response = await user.PutAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderCannotAcceptInvitation()
    {
        // Arrange
        var user = await CreateIntruder();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var passwordResponse = await InviteFriend(home, "admin@gmail.com", "Display");
        var invitation = RequestFactory.CreateAcceptInvitation(passwordResponse.Password);

        // Act
        var response = await user.PutAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvitationExpires()
    {
        // Arrange
        var user = await CreateAdmin();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<HomeModuleOptions>>();
        context.RemoveRange(context.HomeSharings.ToList());

        var passwordResponse = await InviteFriend(home, "admin@gmail.com", "Display");

        var invitation = RequestFactory.CreateAcceptInvitation(passwordResponse.Password);

        // Act
        await Task.Delay(TimeSpan.FromSeconds(options.Value.Sharings.InvitationExpiresInSeconds+1));
        var response = await user.PutAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
