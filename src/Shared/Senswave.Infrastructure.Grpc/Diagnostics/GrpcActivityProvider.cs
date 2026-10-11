using Senswave.Abstractions.Diagnostics;

namespace Senswave.Infrastructure.Grpc.Diagnostics;

public class GrpcActivityProvider() : ActivityProvider(DefaultListenerName)
{
    public const string DefaultListenerName = "Senswave.Grpc";
}
