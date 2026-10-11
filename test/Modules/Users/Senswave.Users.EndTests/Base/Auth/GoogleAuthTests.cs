using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Senswave.Abstractions.Resulting;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.Users.Application.Auth.Google;
using Senswave.Users.Domain.Entity;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Senswave.Users.EndTests.Base.Auth;

[Trait("Collection", "EndTest")]
public class GoogleAuthTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task AutomaticAccountLinking()
    {
        // Arrange
        var (client, email, _) = await CreateClientWithRoleWithoutConsent();
        var validCode = "valid-code";
        var userId = Guid.NewGuid().ToString();
        using var scope = Factory.Server.Services.CreateScope();
        var request = new JsonObject()
        {
            ["Code"] = validCode
        }.Serialize();

        (Factory as BaseTestEnvironment)!.GoogleServiceMock
            .Setup(x => x.RetriveExternalUserInfo(validCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExternalUserDataModel>.Success(
                new ExternalUserDataModel
                {
                    Email = email,
                    Subject = userId
                }));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Act
        var loginResponse = await client.PostAsync($"{Paths.AuthPath}/login/google", request);
        var content = await loginResponse.Content.ReadAsStringAsync();
        var contentData = JsonSerializer.Deserialize<JsonObject>(content);
        var token = contentData?["accessToken"]?.GetValue<string>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var userDataResponse = await client.GetAsync(Paths.UsersPath);
        var userDataContent = await userDataResponse.Content.ReadAsStringAsync();
        var user = await userManager.FindByEmailAsync(email);
        IList<UserLoginInfo> userLogins = [];

        if (user != null)
        {
            userLogins = await userManager.GetLoginsAsync(user);
        }

        var userFoundByExternalId = await userManager.FindByLoginAsync("Google", userId);

        // Assert
        Assert.NotNull(token);
        Assert.True(userDataResponse.IsSuccessStatusCode, $"Response code: {userDataResponse.StatusCode}, Content {userDataContent}");
        Assert.Equal(HttpStatusCode.OK, userDataResponse.StatusCode);
        Assert.NotNull(user);
        Assert.Single(userLogins);
        Assert.NotNull(userFoundByExternalId);
    }

    [Fact]
    public async Task NewUserCanAuthenticate()
    {
        // Arrange
        var validCode = "valid-code";
        var email = $"{Guid.NewGuid()}@gmail.com";
        var userId = Guid.NewGuid().ToString();
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient();
        var request = new JsonObject()
        {
            ["Code"] = validCode
        }.Serialize();

        (Factory as BaseTestEnvironment)!.GoogleServiceMock
            .Setup(x => x.RetriveExternalUserInfo(validCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExternalUserDataModel>.Success(
                new ExternalUserDataModel
                {
                    Email = email,
                    Subject = userId
                }));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Act
        var response = await client.PostAsync($"{Paths.AuthPath}/login/google", request);
        var content = await response.Content.ReadAsStringAsync();
        var contentData = JsonSerializer.Deserialize<JsonObject>(content);
        var token = contentData?["accessToken"]?.GetValue<string>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var userDataResponse = await client.GetAsync(Paths.UsersPath);
        var userDataContent = await userDataResponse.Content.ReadAsStringAsync();
        var userExists = await userManager.FindByEmailAsync(email);
        var userFoundByExternalId = await userManager.FindByLoginAsync("Google", userId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(token);
        Assert.True(userDataResponse.IsSuccessStatusCode, $"Response code: {userDataResponse.StatusCode}, Content {userDataContent}");
        Assert.Equal(HttpStatusCode.OK, userDataResponse.StatusCode);
        Assert.NotNull(userExists);
        Assert.NotNull(userFoundByExternalId);
    }

    [Fact]
    public async Task OldUserCanAuthenticate()
    {
        // Arrange
        var validCode = "valid-code";
        var email = $"{Guid.NewGuid()}@gmail.com";
        var userId = Guid.NewGuid().ToString();
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient();
        var request = new JsonObject()
        {
            ["Code"] = validCode
        }.Serialize();

        (Factory as BaseTestEnvironment)!.GoogleServiceMock
            .Setup(x => x.RetriveExternalUserInfo(validCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExternalUserDataModel>.Success(
                new ExternalUserDataModel
                {
                    Email = email,
                    Subject = userId
                }));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Act
        var registerResponse = await client.PostAsync($"{Paths.AuthPath}/login/google", request);

        var loginResponse = await client.PostAsync($"{Paths.AuthPath}/login/google", request);
        var content = await loginResponse.Content.ReadAsStringAsync();
        var contentData = JsonSerializer.Deserialize<JsonObject>(content);
        var token = contentData?["accessToken"]?.GetValue<string>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var userDataResponse = await client.GetAsync(Paths.UsersPath);
        var userDataContent = await userDataResponse.Content.ReadAsStringAsync();
        var userExists = await userManager.FindByEmailAsync(email);
        var userFoundByExternalId = await userManager.FindByLoginAsync("Google", userId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        Assert.NotNull(token);
        Assert.True(userDataResponse.IsSuccessStatusCode, $"Response code: {userDataResponse.StatusCode}, Content {userDataContent}");
        Assert.Equal(HttpStatusCode.OK, userDataResponse.StatusCode);
        Assert.NotNull(userExists);
        Assert.NotNull(userFoundByExternalId);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [InlineData("SomePassword123!")]
    [InlineData("")]
    public async Task CannotAuthenticateWithPassword(string password)
    {
        // Arrange
        var validCode = "valid-code";
        var email = $"{Guid.NewGuid()}@gmail.com";
        var userId = Guid.NewGuid().ToString();
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient();
        var request = new JsonObject()
        {
            ["Code"] = validCode
        }.Serialize();

        var loginRequest = new JsonObject()
        {
            ["Email"] = email,
            ["Password"] = password
        }.Serialize();

        (Factory as BaseTestEnvironment)!.GoogleServiceMock
            .Setup(x => x.RetriveExternalUserInfo(validCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExternalUserDataModel>.Success(
                new ExternalUserDataModel
                {
                    Email = email,
                    Subject = userId
                }));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Act
        var registerResponse = await client.PostAsync($"{Paths.AuthPath}/login/google", request);
        var loginResponse = await client.PostAsync($"{Paths.AuthPath}/login", request);
        var userExists = await userManager.FindByEmailAsync(email);
        var userFoundByExternalId = await userManager.FindByLoginAsync("Google", userId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
        Assert.NotNull(userExists);
        Assert.NotNull(userFoundByExternalId);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsWhenInvalid()
    {
        // Arrange
        var invalidCode = "invalid-code";
        var email = $"{Guid.NewGuid()}@gmail.com";
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient();
        var request = new JsonObject()
        {
            ["Code"] = invalidCode
        }.Serialize();

        (Factory as BaseTestEnvironment)!.GoogleServiceMock
            .Setup(x => x.RetriveExternalUserInfo(invalidCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExternalUserDataModel>.Failure());

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Act
        var response = await client.PostAsync($"{Paths.AuthPath}/login/google", request);

        var userExists = await userManager.FindByEmailAsync(email);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(userExists);
    }
}
