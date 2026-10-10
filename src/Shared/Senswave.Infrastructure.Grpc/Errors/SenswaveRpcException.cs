using Senswave.Abstractions.Resulting;

namespace Senswave.Infrastructure.Grpc.Errors;

/// <summary>
/// Carries <see cref="Result"/> failures out of a gRPC service method.
/// <see cref="Interceptors.ErrorHandlingInterceptor"/> converts it into an <c>RpcException</c>.
/// </summary>
public sealed class SenswaveRpcException : Exception
{
    public Error[] Errors { get; }

    public SenswaveRpcException(Error[] errors)
        : base(errors.Length > 0 ? errors[0].ToString() : "Operation failed.")
    {
        if (errors.Length == 0)
            throw new ArgumentException("Operation is failure but found no error.", nameof(errors));

        if (errors.Any(x => x == Error.None))
            throw new ArgumentException("Operation is failure but found none error.", nameof(errors));

        Errors = errors;
    }
}
