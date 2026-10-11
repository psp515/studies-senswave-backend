using Microsoft.Extensions.Configuration;

namespace Senswave.Abstractions.Transport;

public sealed class TransportOptions
{
    public const string SectionName = "Transport";

    public TransportMode Mode { get; set; } = TransportMode.Rest;

    /// <summary>
    /// True for every gRPC-based mode (<see cref="TransportMode.Grpc"/> and <see cref="TransportMode.GrpcWeb"/>).
    /// </summary>
    public bool IsGrpc => Mode is TransportMode.Grpc or TransportMode.GrpcWeb;

    /// <summary>
    /// Reads <c>Transport:Mode</c> (env <c>Transport__Mode</c>). Missing value means <see cref="TransportMode.Rest"/>;
    /// an unknown value fails startup rather than silently falling back.
    /// </summary>
    public static TransportOptions FromConfiguration(IConfiguration configuration)
    {
        var value = configuration[$"{SectionName}:{nameof(Mode)}"];

        if (string.IsNullOrWhiteSpace(value))
            return new();

        if (!Enum.TryParse<TransportMode>(value, ignoreCase: true, out var mode) || !Enum.IsDefined(mode))
            throw new InvalidOperationException(
                $"Invalid {SectionName}:{nameof(Mode)} '{value}'. Allowed: {string.Join(", ", Enum.GetNames<TransportMode>())}.");

        return new() { Mode = mode };
    }
}
