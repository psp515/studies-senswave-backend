using Google.Protobuf;
using Grpc.Core;
using Senswave.Abstractions.Resulting;
using Senswave.Infrastructure.Grpc.Interceptors;

namespace Senswave.Infrastructure.Grpc.Errors;

/// <summary>
/// Trailer keys written by <see cref="ErrorHandlingInterceptor"/>. They mirror the
/// <c>errors</c> and <c>traceId</c> fields of the REST <c>ErrorProblemDetails</c>.
/// </summary>
public static class GrpcErrorMetadata
{
    /// <summary>One binary trailer entry per error, each a serialized <c>senswave.common.v1.Error</c>.</summary>
    public const string ErrorsKey = "senswave-errors-bin";

    public const string TraceIdKey = "senswave-trace-id";

    public static Metadata Create(IEnumerable<Error> errors, string? traceId)
    {
        var metadata = new Metadata();

        foreach (var error in errors)
            metadata.Add(ErrorsKey, error.ToContract().ToByteArray());

        if (!string.IsNullOrEmpty(traceId))
            metadata.Add(TraceIdKey, traceId);

        return metadata;
    }

    /// <summary>Reads the errors back from the trailers of a failed call (client side).</summary>
    public static IReadOnlyList<Contracts.Common.V1.Error> GetErrors(this Metadata trailers) => trailers
        .Where(x => x.IsBinary && x.Key == ErrorsKey)
        .Select(x => Contracts.Common.V1.Error.Parser.ParseFrom(x.ValueBytes))
        .ToList();

    public static string? GetTraceId(this Metadata trailers) => trailers.GetValue(TraceIdKey);
}
