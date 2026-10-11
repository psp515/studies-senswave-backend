using Microsoft.Extensions.DependencyInjection;
using Senswave.Homes.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class GetCurrentHomeTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await PostHome();

        // Act
        var response = await client.GetAsync($"{Paths.HomesPath}/current");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await PostHome();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/current?latitude={127}&longitude={-400}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CurrentHomeWithoutLocation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await PostHome();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/current");
        var result = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<GetHomeResponse>(result);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dto?.Id);
    }

    [Fact]
    public async Task CurrentHomeWithoutValidLocation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await PostHome();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/current?latitude=77");
        var result = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<GetHomeResponse>(result);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dto?.Id);
    }

    [Fact]
    public async Task CurrentHomeWithLocation()
    {
        // Arrange
        using var scope = Factory.Server.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();

        var homesToRemove = context.Homes
            .Where(x => x.Location!.Latitude != 51 && x.Location.Longitude != 76)
            .ToList();

        context.RemoveRange(homesToRemove);

        await context.SaveChangesAsync();

        var client = CreateUnauthorizedClient();
        var random = new Random();

        var latitude = random.Next(-90, 50);
        var longitude = random.Next(-180, 70);
        var home = await PostAdminHome(longitude, latitude);

        var userHome = await PostHome();
        await PrepareHomeSharingForAdmin(userHome);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/current?latitude={latitude}&longitude={longitude}");
        var result = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<GetHomeResponse>(result);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(home, dto?.Id);
    }

    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task CurrentHomeIsSharedHome(string privileges)
    {
        // Arrange
        using var scope = Factory.Server.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();

        var homesToRemove = context.Homes
            .Where(x => x.Location!.Latitude != 51 && x.Location.Longitude != 76)
            .ToList();

        context.RemoveRange(homesToRemove);

        await context.SaveChangesAsync();

        var client = CreateUnauthorizedClient();
        var random = new Random();

        var latitude = random.Next(-90, 50);
        var longitude = random.Next(-180, 70);
        var home = await PostHome(longitude, latitude);
        await PrepareHomeSharingForAdmin(home, privileges);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/current?latitude={latitude}&longitude={longitude}");
        var result = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<GetHomeResponse>(result);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(home, dto?.Id);
    }
}