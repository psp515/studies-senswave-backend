using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.Users.Domain.Entity;

namespace Senswave.Users.EndTests.Base.Auth;

[Trait("Collection", "EndTest")]
public class ConfirmEmailV2Tests(BaseTestEnvironment environment) : BaseFeatureTest(environment)
{
    private const string SuccessRedirect = "https://www.google.com/";
    private const string FailureRedirect = "https://www.cisco.com/";

    private async Task UpdateBaseSettings(HttpClient client)
    {
        var baserUrl = $"{client.BaseAddress}{Paths.AuthPathV2}/confirmEmail";

        await UpdateSetting("Modules:Users:Sender:ConfirmationUrl", baserUrl);
        await UpdateSetting("Modules:Users:ConfirmEmailV2:SuccessRedirect", SuccessRedirect);
        await UpdateSetting("Modules:Users:ConfirmEmailV2:FailureRedirect", FailureRedirect);
    }

    [Fact]
    public async Task ConfirmsAccount()
    {
        // Arrange
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient(false);
        var username = $"{Guid.NewGuid()}@test.com";
        var password = "Abecadło712576!!@#$%$";

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var bodyEmail = string.Empty;
        var toEmail = string.Empty;

        (Factory as BaseTestEnvironment)!.EmailServiceMock.Setup(x => x.SendEmailAsync(
                username,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string to, string subject, string body) =>
            {
                bodyEmail = body;
                toEmail = to;

                return Task.CompletedTask;
            });

        await UpdateBaseSettings(client);

        // Act
        await Tests.RegisterClient(client, username, password);

        await Task.Delay(200);

        var doc = new HtmlDocument();
        doc.LoadHtml(bodyEmail);
        var linkNode = doc.DocumentNode.SelectSingleNode("(//a)[3]");
        var confirmationLink = linkNode?.GetAttributeValue("href", string.Empty).Replace("&amp;", "&")!;
        var confirmationResult = await client.GetAsync(confirmationLink);

        var user = await userManager.FindByNameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(username, toEmail);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(HttpStatusCode.Found, confirmationResult.StatusCode);
        Assert.NotNull(confirmationResult.Headers.Location);
        Assert.Equal(SuccessRedirect, confirmationResult.Headers.Location.ToString());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsToConfirmInvalidCodeForUser()
    {
        // Arrange
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient(false);
        var username = $"{Guid.NewGuid()}@test.com";
        var username2 = $"{Guid.NewGuid()}{Guid.NewGuid()}@test.com";
        var password = "Abecadło712576!!@#$%$";

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var bodyEmail = string.Empty;
        var toEmail = string.Empty;

        (Factory as BaseTestEnvironment)!.EmailServiceMock.Setup(x => x.SendEmailAsync(
                It.IsIn(username, username2),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string to, string subject, string body) =>
            {
                bodyEmail = body;
                toEmail = to;

                return Task.CompletedTask;
            });

        await UpdateBaseSettings(client);

        // Act
        await Tests.RegisterClient(client, username, password);
        await Task.Delay(200);
        var user1Text = bodyEmail;

        await Tests.RegisterClient(client, username2, password);
        await Task.Delay(400);
        var user2Text = bodyEmail;

        var doc = new HtmlDocument();
        doc.LoadHtml(user1Text);
        var linkNode = doc.DocumentNode.SelectSingleNode("(//a)[3]");
        var confirmationLink = linkNode?.GetAttributeValue("href", string.Empty).Replace("&amp;", "&")!;

        var doc2 = new HtmlDocument();
        doc2.LoadHtml(user2Text);
        var linkNode2 = doc2.DocumentNode.SelectSingleNode("(//a)[3]");
        var confirmationLink2 = linkNode2?.GetAttributeValue("href", string.Empty).Replace("&amp;", "&")!;

        var uri2 = new Uri(confirmationLink2);
        var queryDict2 = QueryHelpers.ParseQuery(uri2.Query);

        var uri = new Uri(confirmationLink);
        var queryDict = QueryHelpers.ParseQuery(uri.Query);
        queryDict["userId"] = queryDict2["userId"];
        var newQuery = QueryString.Create(queryDict.Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()))!);
        var newUri = uri.GetLeftPart(UriPartial.Path) + newQuery;

        var confirmationResult = await client.GetAsync(newUri);
        var user = await userManager.FindByNameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(HttpStatusCode.Found, confirmationResult.StatusCode);
        Assert.NotNull(confirmationResult.Headers.Location);
        Assert.Equal(FailureRedirect, confirmationResult.Headers.Location.ToString());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsToConfirmUnknownUser()
    {
        // Arrange
        using var scope = Factory.Server.Services.CreateScope();
        var client = CreateUnauthorizedClient(false);
        var username = $"{Guid.NewGuid()}@test.com";
        var password = "Abecadło712576!!@#$%$";

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var bodyEmail = string.Empty;
        var toEmail = string.Empty;

        (Factory as BaseTestEnvironment)!.EmailServiceMock.Setup(x => x.SendEmailAsync(
                username,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string to, string subject, string body) =>
            {
                bodyEmail = body;
                toEmail = to;

                return Task.CompletedTask;
            });

        await UpdateBaseSettings(client);

        // Act
        await Tests.RegisterClient(client, username, password);

        await Task.Delay(200);

        var doc = new HtmlDocument();
        doc.LoadHtml(bodyEmail);
        var linkNode = doc.DocumentNode.SelectSingleNode("(//a)[3]");
        var confirmationLink = linkNode?.GetAttributeValue("href", string.Empty).Replace("&amp;", "&");

        var uri = new Uri(confirmationLink!);
        var queryDict = QueryHelpers.ParseQuery(uri.Query);
        queryDict["userId"] = "notExistingId";
        var newQuery = QueryString.Create(queryDict.Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()))!);
        var newUri = uri.GetLeftPart(UriPartial.Path) + newQuery;

        var confirmationResult = await client.GetAsync(newUri);
        var user = await userManager.FindByNameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(username, toEmail);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(HttpStatusCode.Unauthorized, confirmationResult.StatusCode);
    }
}
