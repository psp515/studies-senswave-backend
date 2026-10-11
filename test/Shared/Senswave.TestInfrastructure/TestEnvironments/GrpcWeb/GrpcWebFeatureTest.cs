using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Senswave.Api;
using Senswave.TestInfrastructure.TestEnvironments.Grpc;

namespace Senswave.TestInfrastructure.TestEnvironments.GrpcWeb;

/// <summary>
/// Base for <c>*.EndTestsGrpcWeb</c> projects: every channel speaks gRPC-Web, so the calls only succeed if the
/// host runs the gRPC-Web middleware. The login and consent helpers of <see cref="GrpcFeatureTestBase"/> apply unchanged.
/// </summary>
public class GrpcWebFeatureTest(WebApplicationFactory<Program> factory) : GrpcFeatureTestBase(factory), IClassFixture<GrpcWebTestEnvironment>
{
    /// <summary>Wire format for channels created afterwards: binary (<c>application/grpc-web</c>) or base64 text.</summary>
    protected GrpcWebMode WebMode { get; set; } = GrpcWebMode.GrpcWeb;

    protected override HttpMessageHandler WrapTransport(HttpMessageHandler handler) => new GrpcWebHandler(WebMode, handler);
}
