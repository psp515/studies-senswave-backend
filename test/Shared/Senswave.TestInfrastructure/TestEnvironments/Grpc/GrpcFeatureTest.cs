using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Senswave.Api;
using Senswave.Contracts.Users.V1;

namespace Senswave.TestInfrastructure.TestEnvironments.Grpc;

/// <summary>
/// Base for <c>*.EndTestsGrpc</c> projects: success-scenario smoke tests over the gRPC transport.
/// </summary>
public class GrpcFeatureTest(WebApplicationFactory<Program> factory) : GrpcFeatureTestBase(factory), IClassFixture<GrpcTestEnvironment>;

/// <summary>
/// <see cref="GrpcFeatureTest"/> with a Mosquitto broker available (<see cref="GrpcMqttTestEnvironment"/>).
/// </summary>
public class GrpcMqttFeatureTest(WebApplicationFactory<Program> factory) : GrpcFeatureTestBase(factory), IClassFixture<GrpcMqttTestEnvironment>
{
    protected GrpcMqttTestEnvironment MqttEnvironment => (GrpcMqttTestEnvironment)Factory;
}

public abstract class GrpcFeatureTestBase(WebApplicationFactory<Program> factory)
{
    protected const string UserEmail = "user@gmail.com";
    protected const string UserPassword = "User123456!";
    protected const string AdminEmail = "admin@gmail.com";
    protected const string AdminPassword = "Admin123456!";

    protected WebApplicationFactory<Program> Factory => factory;

    protected IServiceProvider Services => Factory.Server.Services;

    protected GrpcTestEnvironment Environment => (GrpcTestEnvironment)Factory;

    #region Channels

    protected GrpcChannel CreateChannel() => CreateChannel(Factory.Server.CreateHandler());

    protected GrpcChannel CreateChannel(string accessToken) =>
        CreateChannel(new BearerTokenHandler(accessToken, Factory.Server.CreateHandler()));

    private GrpcChannel CreateChannel(HttpMessageHandler handler) =>
        GrpcChannel.ForAddress(Factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = WrapTransport(handler)
        });

    /// <summary>Hook for the wire format: native gRPC sends as-is, gRPC-Web wraps the handler.</summary>
    protected virtual HttpMessageHandler WrapTransport(HttpMessageHandler handler) => handler;

    protected static T CreateClient<T>(ChannelBase channel) where T : ClientBase<T> =>
        (T)Activator.CreateInstance(typeof(T), channel)!;

    #endregion

    #region Authorization

    /// <summary>Logs in through <c>AuthService.Login</c>; the Identity REST API is not mapped in gRPC modes.</summary>
    protected async Task<AccessTokenResponse> LoginAsync(string email, string password)
    {
        var client = new AuthService.AuthServiceClient(CreateChannel());

        var response = await client.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.False(string.IsNullOrEmpty(response.AccessToken));

        return response;
    }

    /// <summary>Logs in and accepts the latest legal documents, mirroring REST's <c>AuthorizeClientWithConsent</c>.</summary>
    protected async Task<GrpcChannel> CreateAuthorizedChannelWithConsent(string email, string password)
    {
        var token = await LoginAsync(email, password);
        var channel = CreateChannel(token.AccessToken);

        await new UsersService.UsersServiceClient(channel).CreateConsentsAsync(new CreateConsentsRequest());

        return channel;
    }

    /// <summary>Logs in without accepting legal documents. Enough for <c>senswave.users.v1</c> services.</summary>
    protected async Task<GrpcChannel> CreateAuthorizedChannelWithoutConsent(string email, string password)
    {
        var token = await LoginAsync(email, password);
        return CreateChannel(token.AccessToken);
    }

    protected Task<GrpcChannel> CreateAuthorizedChannelAsUser() => CreateAuthorizedChannelWithConsent(UserEmail, UserPassword);

    protected Task<GrpcChannel> CreateAuthorizedChannelAsAdmin() => CreateAuthorizedChannelWithConsent(AdminEmail, AdminPassword);

    protected async Task<T> CreateAuthorizedClientAsUser<T>() where T : ClientBase<T> =>
        CreateClient<T>(await CreateAuthorizedChannelAsUser());

    protected async Task<T> CreateAuthorizedClientAsAdmin<T>() where T : ClientBase<T> =>
        CreateClient<T>(await CreateAuthorizedChannelAsAdmin());

    #endregion
}
