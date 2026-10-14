using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using Refit;
using Senswave.Abstractions.Resulting;
using Senswave.DataSources.Api.Brokers.Brokers.CreateBroker;
using Senswave.DataSources.Domain.Brokers.Brokers.Enums;
using Senswave.DataSources.Domain.Brokers.Brokers.Extensions;
using Senswave.Homes.Api.Homes.Features.CreateHome;
using Senswave.Homes.Api.Homes.Features.SetHomeDataSource;
using Senswave.Homes.Api.Rooms.CreateRoom;
using Senswave.Presentation.Seed.Automations.Interfaces;
using Senswave.Presentation.Seed.DataSources.Clients;
using Senswave.Presentation.Seed.Devices.Interfaces;
using Senswave.Presentation.Seed.Devices.Types;
using Senswave.Presentation.Seed.Homes.Clients;
using Senswave.Presentation.Seed.Seed.Options;
using Senswave.Presentation.Seed.Users.Clients;
using Senswave.Presentation.Seed.Users.Options;
using Senswave.Users.Domain.Entity;
using Senswave.Users.Infrastructure;

namespace Senswave.Presentation.Seed.Seed.Services;

public class InitialSeedBackgroundService(
    IOptions<SeedOptions> seedOptions,
    IIdentityClient identityClient,
    IUserClient userClient,
    IBrokerClient brokerClient,
    IHomeClient homeClient,
    IRoomClient roomClient,
    IDeviceSeedingService deviceSeedingService,
    IServiceProvider serviceProvider,
    IAutomationSeedingService automationSeedingService,
    ILogger<InitialSeedBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var users = seedOptions.Value.Users;

        if (seedOptions.Value.DefaultUser is not null)
            users.Insert(0, seedOptions.Value.DefaultUser);

        users.AddRange(seedOptions.Value.GeneratedUsers.Expand(fallbackBroker: seedOptions.Value.DefaultUser?.Broker));

        if (users.Count == 0)
        {
            logger.LogInformation("No initial seed to run.");
            return;
        }

        logger.LogInformation("Running initial seed for {userCount} users...", users.Count);

        int errorCount = 0;

        foreach (var user in users)
        {
            var result = await SeedUser(user);

            if (!result)
            {
                errorCount++;
            }
        }

        if (errorCount > 0)
        {
            logger.LogError("Initial seed finished with {errorCount} errors.", errorCount);
        }
        else
        {
            logger.LogInformation("Initial seed finished successfully.");
        }

        logger.LogInformation("Initial seed finished.");
    }

    private async Task<Result> SeedUser(UserOptions user)
    {
        var registerUser = await RegisterUser(user);

        if (!registerUser)
        {
            logger.LogError("[User: {user}] Error registering user. User probably exists.", user.Email);
        }

        var loginUser = await LoginUser(user);

        if (!loginUser)
        {
            logger.LogError("[User: {user}] Error logging in user.", user.Email);
            return Result.Failure();
        }

        await userClient.MakeConsent(loginUser.Data.AccessToken);

        try
        {
            if (!user.Broker.Create)
            {
                return Result.Success();
            }

            var brokerRequest = new CreateBrokerRequest
            {
                ClientName = Guid.NewGuid().ToString(),
                Name = user.Broker.Name,

                Port = user.Broker.Port,
                ProtocolVersion = BrokerProtocolVersion.MqttV5.FromProtocol(),
                Url = user.Broker.Url,
                UseTls = true,

                Password = user.Broker.Password,
                Username = user.Broker.Username
            };

            var brokerCreationResult = await brokerClient.CreateBroker(loginUser.Data.AccessToken, brokerRequest);

            logger.LogInformation("[User: {user}] User broker seeded.", user.Email);

            if (!user.Home.Create)
            {
                return Result.Success();
            }

            var craeteHomeRequest = new CreateHomeRequest
            {
                Name = user.Home.Name,
                DataSourceId = brokerCreationResult.Id,
                Icon = user.Home.Icon,
                Latitude = user.Home.Latitude,
                Longitude = user.Home.Longitude
            };

            var homeCreatedResult = await homeClient.CreateHome(loginUser.Data.AccessToken, craeteHomeRequest);

            var createRoomRequest = new CreateRoomRequest
            {
                Name = "Pokój Łukasza"
            };

            var roomResponse = await roomClient.CreateRoom(loginUser.Data.AccessToken, homeCreatedResult.Id, createRoomRequest);

            logger.LogInformation("[User: {user}] User home seeded.", user.Email);

            var putHomeBrokerRequest = new AssignHomeDataSourceRequest
            {
                BrokerId = brokerCreationResult.Id
            };

            await homeClient.AssignBrokerToHome(loginUser.Data.AccessToken, homeCreatedResult.Id, putHomeBrokerRequest);

            if (user.Devices.EmptyDevice)
            {
                var emptyDevice = await deviceSeedingService.SeedEmptyDevice(loginUser.Data.AccessToken, homeCreatedResult.Id);

                if (!emptyDevice)
                {
                    logger.LogError("[User: {user}] Error seeding empty device.", user.Email);
                    return Result.Failure();
                }

                logger.LogInformation("[User: {user}] Empty device seeded.", user.Email);
            }

            DeviceOperations? detector = null;
            if (user.Devices.Detector)
            {
                var seedDetector = await deviceSeedingService.SeedDetector(loginUser.Data.AccessToken, homeCreatedResult.Id);

                if (!seedDetector)
                {
                    logger.LogError("[User: {user}] Error seeding detector.", user.Email);
                    return Result.Failure();
                }

                detector = seedDetector.Data;

                logger.LogInformation("[User: {user}] Detector seeded.", user.Email);
            }

            DeviceOperations? picoController = null;
            if (user.Devices.PicoController.Enabled)
            {
                var seedPicoController = await deviceSeedingService.SeedPicoController(loginUser.Data.AccessToken, homeCreatedResult.Id, roomResponse.Id, user.Devices.PicoController.GlobalTopic);

                if (!seedPicoController)
                {
                    logger.LogError("[User: {user}] Error seeding Pico controller.", user.Email);
                    return Result.Failure();
                }
                picoController = seedPicoController.Data;

                logger.LogInformation("[User: {user}] Pico controller seeded.", user.Email);
            }

            if (user.Devices.PlantMonitor)
            {
                var plantMonitor = await deviceSeedingService.SeedPlantMonitor(loginUser.Data.AccessToken, homeCreatedResult.Id);

                if (!plantMonitor)
                {
                    logger.LogError("[User: {user}] Error seeding plant monitor.", user.Email);
                    return Result.Failure();
                }

                logger.LogInformation("[User: {user}] Plant monitor seeded.", user.Email);
            }

            if (user.Automations.Create)
            {
                if (picoController is not null)
                {
                    var discoModeAutomationResult = await automationSeedingService.SeedDiscoMode(
                        loginUser.Data.AccessToken,
                        homeCreatedResult.Id,
                        picoController.OptionOperationId,
                        picoController.NumberOperationId);
                    if (!discoModeAutomationResult)
                        return Result.Failure();

                    logger.LogInformation("[User: {user}] Automation Disco Mode seeded", user.Email);

                    if (detector is not null)
                    {
                        var movementDetectionResult = await automationSeedingService.SeedMovementDetection(
                            loginUser.Data.AccessToken,
                            homeCreatedResult.Id,
                            detector.BooleanOperationId,
                            picoController.BooleanOperationId,
                            picoController.OptionOperationId,
                            picoController.NumberOperationId);
                        if (!movementDetectionResult)
                            return Result.Failure();

                        logger.LogInformation("[User: {user}] Automation Movementent Detection Seeded", user.Email);
                    }
                }
            }

            return Result.Success();
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "[User: {user}] Error seeding user.", user.Email);
            return Result.Failure();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[User: {user}] Unknown error seeding user.", user.Email);
            return Result.Failure();
        }
        finally
        {
            logger.LogInformation("[User: {user}] User seed finished.", user.Email);
        }
    }

    private async Task<Result> RegisterUser(UserOptions user)
    {
        try
        {
            var registerRequest = new RegisterRequest
            {
                Email = user.Email,
                Password = user.Password
            };

            await RateLimitRetry.ExecuteAsync(() => identityClient.Register(registerRequest), logger, $"Register {user.Email}");

            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<UsersContext>();

            User currentUser = context.Users.First(u => u.Email == registerRequest.Email);

            currentUser.EmailConfirmed = true;

            context.Users.Update(currentUser);
            await context.SaveChangesAsync();

            return Result.Success();
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "[User: {user}] Error registering user.", user.Email);
            return Result.Failure();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[User: {user}] Unknown error registering user.", user.Email);
            return Result.Failure();
        }
    }

    private async Task<Result<AccessTokenResponse>> LoginUser(UserOptions user)
    {
        try
        {
            var loginRequest = new LoginRequest
            {
                Email = user.Email,
                Password = user.Password
            };

            var response = await RateLimitRetry.ExecuteAsync(() => identityClient.Login(loginRequest), logger, $"Login {user.Email}");

            return Result<AccessTokenResponse>.Success(response);
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "[User: {user}] Error logging in user.", user.Email);
            return Result<AccessTokenResponse>.Failure();
        }
    }
}
