using Grpc.Core;
using Senswave.Abstractions.Resulting;
using System.ComponentModel;
using ContractErrorType = Senswave.Contracts.Common.V1.ErrorType;

namespace Senswave.Infrastructure.Grpc.Errors;

/// <summary>
/// gRPC counterpart of <c>ErrorTypeExtensions</c> in <c>Senswave.Infrastructure.Web</c>.
/// </summary>
public static class ErrorTypeGrpcExtensions
{
    public static StatusCode GetGrpcStatusCode(this ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCode.InvalidArgument,
        ErrorType.NotFound => StatusCode.NotFound,
        ErrorType.Conflict => StatusCode.AlreadyExists,
        ErrorType.Failure => StatusCode.FailedPrecondition,
        ErrorType.ServerFail => StatusCode.Internal,
        _ => throw new InvalidEnumArgumentException()
    };

    public static string GetGrpcTitle(this ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Bad Request",
        ErrorType.NotFound => "Not Found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Failure => "Bad Request",
        ErrorType.ServerFail => "Server Error",
        _ => throw new InvalidEnumArgumentException()
    };

    public static ContractErrorType ToContract(this ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => ContractErrorType.Validation,
        ErrorType.NotFound => ContractErrorType.NotFound,
        ErrorType.Conflict => ContractErrorType.Conflict,
        ErrorType.Failure => ContractErrorType.Failure,
        ErrorType.ServerFail => ContractErrorType.ServerFail,
        _ => throw new InvalidEnumArgumentException()
    };

    public static Contracts.Common.V1.Error ToContract(this Error error) => new()
    {
        Code = error.Code,
        Type = error.Type.ToContract(),
        Description = error.Description,
    };
}
