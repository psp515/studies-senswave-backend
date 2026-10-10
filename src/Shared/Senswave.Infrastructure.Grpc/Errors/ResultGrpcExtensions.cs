using Senswave.Abstractions.Resulting;

namespace Senswave.Infrastructure.Grpc.Errors;

public static class ResultGrpcExtensions
{
    /// <summary>
    /// gRPC counterpart of <c>ToResultsDetails</c>: throws a <see cref="SenswaveRpcException"/> when the result is a failure.
    /// </summary>
    public static void ThrowIfFailure(this Result result)
    {
        if (result.IsSuccess)
            return;

        throw new SenswaveRpcException(result.Errors);
    }
}
