using Senswave.Abstractions.Transport;
using Senswave.TestInfrastructure.Fixtures.Database;
using Senswave.TestInfrastructure.Fixtures.MessageBus;
using Senswave.TestInfrastructure.TestEnvironments.Grpc;

namespace Senswave.TestInfrastructure.TestEnvironments.GrpcWeb;

/// <summary>
/// Host running with <c>Transport:Mode=GrpcWeb</c> against its own database. Everything else (mocks, options,
/// seeded users) is inherited from <see cref="GrpcTestEnvironment"/>, since gRPC-Web serves the same services.
/// </summary>
public class GrpcWebTestEnvironment : GrpcTestEnvironment
{
#if TESTS
    private static readonly IDatabase GrpcWebDatabase = new PostgreSqlFixture("SenswaveEndTestsGrpcWeb");
    private static readonly IMessageBus GrpcWebMessageBus = new RabbitMqFixture();
#else
    private static readonly IDatabase GrpcWebDatabase = new PostgreSqlContainerFixture("SenswaveEndTestsGrpcWeb");
    private static readonly IMessageBus GrpcWebMessageBus = new RabbitMqContainerFixture();
#endif

    protected override TransportMode Mode => TransportMode.GrpcWeb;

    protected override IDatabase Database => GrpcWebDatabase;

    protected override IMessageBus MessageBus => GrpcWebMessageBus;
}
