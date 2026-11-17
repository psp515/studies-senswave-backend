using Perf.Core;

namespace Perf.Client;

public static class ApiClientFactory
{
    public static IApiClient Create(Transport transport, ApiTarget target) => transport switch
    {
        Transport.Rest => new Rest.RestApiClient(target),
        Transport.Grpc => new Grpc.GrpcApiClient(target, web: false),
        Transport.GrpcWeb => new Grpc.GrpcApiClient(target, web: true),
        _ => throw new ArgumentOutOfRangeException(nameof(transport)),
    };
}
