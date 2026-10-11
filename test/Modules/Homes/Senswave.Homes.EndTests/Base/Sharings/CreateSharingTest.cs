using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Homes.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using Senswave.Users.Infrastructure;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class CreateSharingTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var user = CreateUnauthorizedClient();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CanPostInvitation()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "admin@gmail.com",
            sharingType: "Display").Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation);
        var data = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonSerializer.Deserialize<InviteFriendResponse>(data);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(jsonResponse);
        Assert.NotNull(jsonResponse.Password);
        Assert.NotEmpty(jsonResponse.Password);
    }

    [Fact]
    public async Task PasswordIsHashed()
    {
        // Arrange
        var user = await CreateUser();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "admin@gmail.com",
            sharingType: "Display").Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation);
        var data = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonSerializer.Deserialize<InviteFriendResponse>(data)!;

        var invitationEntity = await context.HomeInvitations
            .Where(x => x.Id == jsonResponse.InvitationId)
            .FirstAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(jsonResponse);
        Assert.NotNull(jsonResponse.Password);
        Assert.NotEqual(jsonResponse.Password, invitationEntity.Password);
    }

    [Fact]
    public async Task InvitationIsOverriden()
    {
        // Arrange
        var user = await CreateUser();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "admin@gmail.com",
            sharingType: _displayPrivilege);

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());
        var data = await response.Content.ReadAsStringAsync();
        var firstResponse = JsonSerializer.Deserialize<InviteFriendResponse>(data);

        var duplicateResponse = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());
        var data2 = await duplicateResponse.Content.ReadAsStringAsync();
        var secondResponse = JsonSerializer.Deserialize<InviteFriendResponse>(data2)!;

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);
        Assert.NotNull(firstResponse);
        Assert.NotNull(secondResponse);
        Assert.NotEqual(firstResponse.InvitationId, secondResponse.InvitationId);
    }

    [Fact]
    public async Task OneInvitationAvailableInDatabase()
    {
        // Arrange
        var user = await CreateUser();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var homesContext = scope.ServiceProvider.GetRequiredService<HomesContext>();
        homesContext.RemoveRange(homesContext.HomeSharings.ToList());

        var userContext = scope.ServiceProvider.GetRequiredService<UsersContext>();

        var friendEmail = "admin@gmail.com";
        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: friendEmail,
            sharingType: _displayPrivilege);

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());
        var resopnse2 = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());
        var resopnse3 = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation.Serialize());

        var friendId = await userContext.Users
            .Where(x => x.Email == friendEmail)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        var invitations = await homesContext.HomeInvitations
            .Where(x => x.HomeId == home)
            .Where(x => x.FriendId == friendId)
            .ToListAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, resopnse2.StatusCode);
        Assert.Equal(HttpStatusCode.Created, resopnse3.StatusCode);
        Assert.Single(invitations);
    }

    [Fact]
    public async Task CaseInsensitiveEmail()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "Admin@gmail.com",
            sharingType: "Display").Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation);
        var data = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonSerializer.Deserialize<InviteFriendResponse>(data);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(jsonResponse);
        Assert.NotNull(jsonResponse.Password);
        Assert.NotEmpty(jsonResponse.Password);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "admin@gmail.com",
            sharingType: "").Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderCannotCreateInvitation()
    {
        // Arrange
        var user = await CreateIntruder();
        var home = await PostHome();

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "admin@gmail.com",
            sharingType: "Display").Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task FriendCannotCreateInvitation(string privilege)
    {
        // Arrange
        var user = await CreateAdmin();
        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home, privilege);

        using var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        context.RemoveRange(context.HomeSharings.ToList());

        var invitation = RequestFactory.CreateFriendInvitation(
            homeId: home,
            friendEmail: "admin@gmail.com",
            sharingType: "Display").Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.HomesPath}/sharings", invitation);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}