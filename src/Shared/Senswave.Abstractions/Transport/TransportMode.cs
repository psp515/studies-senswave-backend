namespace Senswave.Abstractions.Transport;

/// <summary>
/// API surface served by the host. Exactly one is live at a time.
/// </summary>
public enum TransportMode
{
    Rest,
    Grpc,
    GrpcWeb
}
