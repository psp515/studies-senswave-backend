using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Senswave.Infrastructure.Grpc.Diagnostics;
using Senswave.Infrastructure.Grpc.Errors;
using System.Diagnostics;

namespace Senswave.Infrastructure.Grpc.Interceptors;

/// <summary>
/// Translates <see cref="SenswaveRpcException"/> into gRPC statuses and acts as the gRPC equivalent of
/// <c>GlobalExceptionHandlerMiddleware</c>: exceptions thrown inside a gRPC service never reach ASP.NET Core's exception handler.
/// </summary>
public sealed class ErrorHandlingInterceptor(
    ILogger<ErrorHandlingInterceptor> logger,
    GrpcActivityProvider activityProvider) : Interceptor
{
    public const string ErrorTypeTag = "senswave.error_type";
    public const string StatusCodeTag = "rpc.grpc.status_code";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var activity = activityProvider.StartActivity(context.Method);

        try
        {
            return await continuation(request, context);
        }
        catch (Exception exception) when (exception is not RpcException && !IsClientCancellation(exception, context))
        {
            throw Translate(exception, activity);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var activity = activityProvider.StartActivity(context.Method);

        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception exception) when (exception is not RpcException && !IsClientCancellation(exception, context))
        {
            throw Translate(exception, activity);
        }
    }

    private RpcException Translate(Exception exception, Activity? activity)
    {
        var traceId = Activity.Current?.Id;

        if (exception is SenswaveRpcException senswaveException)
        {
            var errorType = senswaveException.Errors[0].Type;
            var statusCode = errorType.GetGrpcStatusCode();

            activity?.SetTag(ErrorTypeTag, errorType.ToString());
            activity?.SetTag(StatusCodeTag, (int)statusCode);
            activity?.SetStatus(ActivityStatusCode.Error);

            return new RpcException(
                new Status(statusCode, errorType.GetGrpcTitle()),
                GrpcErrorMetadata.Create(senswaveException.Errors, traceId));
        }

        logger.LogError("Time: {@DateTimeUtc}, Exception: {@ExceptioName}, Exception Message: {ExceptionMessage}",
            DateTime.UtcNow,
            nameof(exception),
            exception.Message);

        activity?.SetTag(StatusCodeTag, (int)StatusCode.Internal);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        return new RpcException(
            new Status(StatusCode.Internal, "Server Error"),
            GrpcErrorMetadata.Create([], traceId));
    }

    private static bool IsClientCancellation(Exception exception, ServerCallContext context) =>
        exception is OperationCanceledException && context.CancellationToken.IsCancellationRequested;
}
