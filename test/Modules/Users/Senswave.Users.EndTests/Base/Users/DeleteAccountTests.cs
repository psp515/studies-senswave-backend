using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Senswave.Automations.Infrastructure;
using Senswave.DataSources.Infrastructure;
using Senswave.Devices.Infrastructure;
using Senswave.Homes.Infrastructure;
using Senswave.Infrastructure.Module;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Automations;
using Senswave.Users.Infrastructure;
using System.Text.Json.Nodes;

namespace Senswave.Users.EndTests.Base.Users;

[Trait("Collection", "EndTest")]
public class DeleteAccountTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    public const string Path = $"{Paths.UsersPath}/account";

    private Task EnableWorker() => UpdateSetting("Modules:Users:DeleteAccount:WorkerEnabled", true);
    private Task DisableWorker() => UpdateSetting("Modules:Users:DeleteAccount:WorkerEnabled", false);

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.DeleteAsync(Path);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NumberOfModulesWithDeleteUserActions()
    {
        // Arrange
        var expectedNumberOfModules = 6;


        // Act
        var senswaveAssemblies = ModuleLoader.LoadAssemblies();
        var modules = ModuleLoader.LoadModules(senswaveAssemblies);

        // Assert
        Assert.Equal(expectedNumberOfModules, modules.Count);
    }

    [Fact]
    public async Task UserDataHashed()
    {
        // Arrange
        var (client, email, password) = await CreateClientWithConsent();
        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        bool userExistsBefore = false;
        HttpResponseMessage? response;
        bool userDoesNotExistAfter = false;
        bool removedUserExists = false;

        var emailTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        (Factory as BaseTestEnvironment)!.EmailServiceMock
            .Setup(x => x.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string to, string subject, string body) =>
            {
                emailTcs.TrySetResult(true);
                return Task.CompletedTask;
            });

        // Act
        await DisableWorker();
        await Task.Delay(6000);

        try
        {
            userExistsBefore = await context.Users
               .AnyAsync(x => x.Email == email);

            response = await client.DeleteAsync(Path);

            userDoesNotExistAfter = await context.Users
               .AnyAsync(x => x.Email == email);

            removedUserExists = await context.RemovedUsers
               .AnyAsync(x => x.HashedEmail == email);
        }
        finally
        {
            await EnableWorker();
        }

        var emailCompleted = await Task.WhenAny(emailTcs.Task, Task.Delay(12000));
        bool emailSend = emailCompleted == emailTcs.Task;

        await Task.Delay(6000);

        var userFoundAfterHashing = await context.RemovedUsers
               .AnyAsync(x => x.HashedEmail == email);

        // Assert
        Assert.True(userExistsBefore);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(userDoesNotExistAfter);
        Assert.True(removedUserExists);
        Assert.True(emailSend);
        Assert.False(userFoundAfterHashing);

        await Assert.ThrowsAnyAsync<HttpRequestException>(async () =>
        {
            await client.PostLogin(email, password);
        });
    }

    [Fact]
    public async Task UserDataRemoved()
    {
        // Arrange
        var (client, email, password) = await CreateClientWithConsent();
        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        bool userExistsBefore = false;
        HttpResponseMessage? response;
        bool userDoesNotExistAfter = false;
        bool removedUserExists = false;

        // Act
        await DisableWorker();
        await Task.Delay(2000);

        try
        {
            userExistsBefore = await context.Users
               .AnyAsync(x => x.Email == email);

            response = await client.DeleteAsync(Path);

            userDoesNotExistAfter = await context.Users
               .AnyAsync(x => x.Email == email);

            removedUserExists = await context.RemovedUsers
               .AnyAsync(x => x.HashedEmail == email);
        }
        finally
        {
            await EnableWorker();
            await Task.Delay(2000);
        }

        // Assert
        Assert.True(userExistsBefore);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(userDoesNotExistAfter);
        Assert.True(removedUserExists);

        await Assert.ThrowsAnyAsync<HttpRequestException>(async () =>
        {
            await client.PostLogin(email, password);
        });
    }

    [Fact]
    public async Task DataSourcesDataRemoved()
    {
        // Arrange
        var (client, _, _) = await CreateClientWithConsent();
        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSourcesContext>();
        var devicesContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        var dataSourceId = await client.PostDataSourceBroker();
        var homeId = await client.PostHome();
        await client.PutDataSourceForHome(dataSourceId, homeId);
        var deviceId = await client.PostDevice(homeId, Guid.Empty);
        var operationId = await client.PostBooleanOperation(deviceId);
        var operation = await devicesContext.Operations
            .Include(x => x.DataReference)
            .FirstAsync(x => x.Id == operationId);

        var subscribtion = await context.Subscribtions
            .FirstAsync(x => x.Id == operation.DataReference!.DataSourceDataReferenceId);

        var response = await client.DeleteAsync(Path);

        var doesNotExistAfter = await context.Brokers
            .AnyAsync(x => x.Id == dataSourceId);

        var subscribtionDoesNotExistAfter = await context.Subscribtions
            .AnyAsync(x => x.Id == subscribtion.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(doesNotExistAfter);
        Assert.False(subscribtionDoesNotExistAfter);
    }

    [Fact]
    public async Task HomesDataRemoved()
    {
        // Arrange
        var (client, _, _) = await CreateClientWithConsent();
        var (sharedClient, sharedClientEmail, _) = await CreateClientWithConsent();
        var (invitedClient, invitedClientEmail, _) = await CreateClientWithConsent();
        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();

        // Act
        var dataSourceId = await client.PostDataSourceBroker();
        var homeId = await client.PostHome();
        var roomId = await client.PostRoom(homeId, "test1");
        await client.PutDataSourceForHome(dataSourceId, homeId);

        var invitationResponse1 = await client.PostHomeInvitation(homeId, sharedClientEmail);
        var invitationResponse2 = await client.PostHomeInvitation(homeId, invitedClientEmail);
        await sharedClient.AcceptHomeInvitation(invitationResponse1.Password);

        var response = await client.DeleteAsync(Path);

        var doesNotExistAfter = await context.Homes
            .AnyAsync(x => x.Id == homeId);

        var roomDoesNotExistAfter = await context.Rooms
            .AnyAsync(x => x.Id == roomId);

        var invitation2NotExistAfter = await context.HomeInvitations
            .AnyAsync(x => x.Id == invitationResponse2.InvitationId);

        var sharingDoesNotExist = await context.HomeSharings
            .Where(x => x.HomeId == homeId)
            .AnyAsync();

        var dataSourceReferenceNotExists = await context.DataSourceReferences
            .AnyAsync(x => x.HomeId == homeId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(doesNotExistAfter);
        Assert.False(roomDoesNotExistAfter);
        Assert.False(invitation2NotExistAfter);
        Assert.False(sharingDoesNotExist);
        Assert.False(dataSourceReferenceNotExists);
    }

    [Fact]
    public async Task HomeDataNotOwnedNotRemoved()
    {
        // Arrange
        var (owner, _, _) = await CreateClientWithConsent();
        var (sharingClientToRemove, sharingClientToRemoveEmail, _) = await CreateClientWithConsent();
        var (sharingClient, sharingClientEmail, _) = await CreateClientWithConsent();

        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomesContext>();
        var usersContext = scope.ServiceProvider.GetRequiredService<UsersContext>();

        // Act
        var dataSourceId = await owner.PostDataSourceBroker();
        var homeId = await owner.PostHome();
        var roomId = await owner.PostRoom(homeId, "test1");
        await owner.PutDataSourceForHome(dataSourceId, homeId);

        var invitationResponse2 = await owner.PostHomeInvitation(homeId, sharingClientEmail);
        await sharingClient.AcceptHomeInvitation(invitationResponse2.Password);

        var invitationResponse1 = await owner.PostHomeInvitation(homeId, sharingClientToRemoveEmail);
        await sharingClientToRemove.AcceptHomeInvitation(invitationResponse1.Password);

        var response = await sharingClientToRemove.DeleteAsync(Path);

        var homeExists = await context.Homes
            .AnyAsync(x => x.Id == homeId);

        var roomExists = await context.Rooms
            .AnyAsync(x => x.Id == roomId);

        var correctSharingsExists = await context.HomeSharings
            .Where(x => x.HomeId == homeId)
            .ToListAsync();

        var dataSourceReferenceExists = await context.DataSourceReferences
            .AnyAsync(x => x.HomeId == homeId);

        var sharedUserEntity = await usersContext.Users
            .FirstAsync(x => x.Email == sharingClientEmail);


        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(homeExists);
        Assert.True(roomExists);
        Assert.True(dataSourceReferenceExists);
        Assert.Single(correctSharingsExists);
        Assert.Equal(sharedUserEntity.Id, correctSharingsExists.First().UserId);
    }

    [Fact]
    public async Task DevicesDataRemoved()
    {
        // Arrange
        var (client, _, _) = await CreateClientWithConsent();
        var (sharedClient, sharedClientEmail, _) = await CreateClientWithConsent();
        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        var dataSourceId = await client.PostDataSourceBroker();
        var homeId = await client.PostHome();
        var roomId = await client.PostRoom(homeId, "test1");
        await client.PutDataSourceForHome(dataSourceId, homeId);

        var deviceId = await client.PostDevice(homeId, roomId);
        var operationId = await client.PostBooleanOperation(deviceId);
        var widgetId = await client.PostBooleanButtonWidget(operationId);
        var dashboardId = await client.PostDashboard(deviceId);
        await client.SetWidgetOnDashboard(dashboardId, widgetId);
        await client.SetSwitchTile(deviceId, operationId);

        var invitationResponse1 = await client.PostHomeInvitation(homeId, sharedClientEmail);
        await sharedClient.AcceptHomeInvitation(invitationResponse1.Password);

        await client.PutDeviceSharing(deviceId, sharedClientEmail, "Display");

        var response = await client.DeleteAsync(Path);

        var doesNotExistAfter = await context.Devices
            .AnyAsync(x => x.Id == deviceId);

        var operationDoesNotExistAfter = await context.Operations
            .AnyAsync(x => x.Id == operationId);

        var widgetDoesNotExistAfter = await context.Widgets
            .AnyAsync(x => x.Id == widgetId);

        var dashboardDoesNotExistAfter = await context.Dashboards
            .AnyAsync(x => x.Id == dashboardId);

        var deviceTileDoesNotExistAfter = await context.DeviceTiles
            .AnyAsync(x => x.DeviceId == deviceId);

        var dataSourceReferencesDoesNotExist = await context.DataReferences
            .AnyAsync(x => x.DeviceId == deviceId);

        var sharingsDoesNotExists = await context.DeviceSharings
            .AnyAsync(x => x.DeviceId == deviceId);

        var homesReferencesDoesNotExist = await context.HomeReferences
            .AnyAsync(x => x.Devices.Any(d => d.Id == deviceId));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(doesNotExistAfter);
        Assert.False(operationDoesNotExistAfter);
        Assert.False(widgetDoesNotExistAfter);
        Assert.False(dashboardDoesNotExistAfter);
        Assert.False(deviceTileDoesNotExistAfter);
        Assert.False(dashboardDoesNotExistAfter);
        Assert.False(dataSourceReferencesDoesNotExist);
        Assert.False(sharingsDoesNotExists);
        Assert.False(homesReferencesDoesNotExist);
    }

    [Fact]
    public async Task DevicesDataNotOwnedNotRemoved()
    {
        // Arrange
        var (owner, _, _) = await CreateClientWithConsent();
        var (sharingClientToRemove, sharingClientToRemoveEmail, _) = await CreateClientWithConsent();
        var (sharingClient, sharingClientEmail, _) = await CreateClientWithConsent();

        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DevicesContext>();
        var usersContext = scope.ServiceProvider.GetRequiredService<UsersContext>();

        // Act
        var dataSourceId = await owner.PostDataSourceBroker();
        var homeId = await owner.PostHome();
        var roomId = await owner.PostRoom(homeId, "test1");
        await owner.PutDataSourceForHome(dataSourceId, homeId);

        var deviceId = await owner.PostDevice(homeId, roomId);
        var operationId = await owner.PostBooleanOperation(deviceId);
        var widgetId = await owner.PostBooleanButtonWidget(operationId);
        var dashboardId = await owner.PostDashboard(deviceId);
        await owner.SetWidgetOnDashboard(dashboardId, widgetId);
        await owner.SetSwitchTile(deviceId, operationId);

        var invitationResponse2 = await owner.PostHomeInvitation(homeId, sharingClientEmail);
        await sharingClient.AcceptHomeInvitation(invitationResponse2.Password);

        var invitationResponse1 = await owner.PostHomeInvitation(homeId, sharingClientToRemoveEmail);
        await sharingClientToRemove.AcceptHomeInvitation(invitationResponse1.Password);

        await owner.PutDeviceSharing(deviceId, sharingClientToRemoveEmail, "Display");
        await owner.PutDeviceSharing(deviceId, sharingClientEmail, "Display");

        var response = await sharingClientToRemove.DeleteAsync(Path);

        var deviceExists = await context.Devices
            .AnyAsync(x => x.Id == deviceId);

        var operationExists = await context.Operations
            .AnyAsync(x => x.Id == operationId);

        var widgetExists = await context.Widgets
            .AnyAsync(x => x.Id == widgetId);

        var dashboardExists = await context.Dashboards
            .AnyAsync(x => x.Id == dashboardId);

        var deviceTileExists = await context.DeviceTiles
            .AnyAsync(x => x.DeviceId == deviceId);

        var dataSourceReferenceExists = await context.DataReferences
            .AnyAsync(x => x.DeviceId == deviceId);

        var correctSharingsExists = await context.DeviceSharings
            .Where(x => x.DeviceId == deviceId)
            .ToListAsync();

        var homeReferenceExists = await context.HomeReferences
            .AnyAsync(x => x.Devices.Any(d => d.Id == deviceId));

        var sharedUserEntity = await usersContext.Users
            .FirstAsync(x => x.Email == sharingClientEmail);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(deviceExists);
        Assert.True(operationExists);
        Assert.True(widgetExists);
        Assert.True(dashboardExists);
        Assert.True(deviceTileExists);
        Assert.True(homeReferenceExists);
        Assert.True(dataSourceReferenceExists);
        Assert.Single(correctSharingsExists);
        Assert.Equal(sharedUserEntity.Id, correctSharingsExists.First().UserId);
    }

    [Fact]
    public async Task AutomationsDataRemoved()
    {
        // Arrange
        var (client, _, _) = await CreateClientWithConsent();
        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        // Act
        var dataSourceId = await client.PostDataSourceBroker();
        var homeId = await client.PostHome();
        var roomId = await client.PostRoom(homeId, "test1");
        await client.PutDataSourceForHome(dataSourceId, homeId);

        var deviceId = await client.PostDevice(homeId, roomId);
        var operationId = await client.PostBooleanOperation(deviceId);
        var operationId2 = await client.PostBooleanOperation(deviceId);

        var automationId = await client.PostAutomation(homeId,
            new List<PostConditionItemRequest>
            {
                new()
                {
                    OperationId = operationId,
                    ConditionType = "BooleanCondition",
                    ConditionConfiguration = new()
                    {
                        { "isOn", true }
                    }
                }
            },
            new List<PostResultItemRequest>
            {
                new()
                {
                    OperationId = operationId2,
                    ValueToSend = JsonValue.Create(true)
                }
            });

        var response = await client.DeleteAsync(Path);

        var doesNotExistAfter = await context.Automations
            .AnyAsync(x => x.Id == automationId);

        var conditionDoesNotExistAfter = await context.AutomationConditions
            .AnyAsync(x => x.Automation.Id == automationId);

        var resultDoesNotExistAfter = await context.AutomationResults
            .AnyAsync(x => x.Automation.Id == automationId);

        var homerRefDoesNotExistAfter = await context.HomeReferences
            .AnyAsync(x => x.Automations.Any(x => x.Id == automationId));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(doesNotExistAfter);
        Assert.False(conditionDoesNotExistAfter);
        Assert.False(resultDoesNotExistAfter);
        Assert.False(homerRefDoesNotExistAfter);
    }

    [Fact]
    public async Task AutomationsDataNotOwnedNotRemovedRemoved()
    {
        // Arrange
        var (owner, _, _) = await CreateClientWithConsent();
        var (sharingClientToRemove, sharingClientToRemoveEmail, _) = await CreateClientWithConsent();
        var (sharingClient, sharingClientEmail, _) = await CreateClientWithConsent();

        var scope = Factory.Server.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        // Act
        var dataSourceId = await owner.PostDataSourceBroker();
        var homeId = await owner.PostHome();
        var roomId = await owner.PostRoom(homeId, "test1");
        await owner.PutDataSourceForHome(dataSourceId, homeId);

        var deviceId = await owner.PostDevice(homeId, roomId);
        var operationId = await owner.PostBooleanOperation(deviceId);
        var operationId2 = await owner.PostBooleanOperation(deviceId);

        var automationId = await owner.PostAutomation(homeId,
            new List<PostConditionItemRequest>
            {
                new()
                {
                    OperationId = operationId,
                    ConditionType = "BooleanCondition",
                    ConditionConfiguration = new()
                    {
                        { "isOn", true }
                    }
                }
            },
            new List<PostResultItemRequest>
            {
                new()
                {
                    OperationId = operationId2,
                    ValueToSend = JsonValue.Create(true)
                }
            });

        var response = await sharingClientToRemove.DeleteAsync(Path);

        var automationExists = await context.Automations
            .AnyAsync(x => x.Id == automationId);

        var conditionExists = await context.AutomationConditions
            .AnyAsync(x => x.Automation.Id == automationId);

        var resultExists = await context.AutomationResults
            .AnyAsync(x => x.Automation.Id == automationId);

        var homeReferenceExists = await context.HomeReferences
            .AnyAsync(x => x.Automations.Any(x => x.Id == automationId));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(automationExists);
        Assert.True(conditionExists);
        Assert.True(resultExists);
        Assert.True(homeReferenceExists);
    }
}
