using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.DataSources.Api.Brokers.Brokers.CreateBroker;
using Senswave.DataSources.Api.Brokers.Brokers.CreateSubscribtion;
using Senswave.DataSources.Api.Brokers.Brokers.DeleteBroker;
using Senswave.DataSources.Api.Brokers.Brokers.DeleteSubscribtion;
using Senswave.DataSources.Api.Brokers.Brokers.GetBroker;
using Senswave.DataSources.Api.Brokers.Brokers.GetBrokers;
using Senswave.DataSources.Api.Brokers.Brokers.GetSubscribtions;
using Senswave.DataSources.Api.Brokers.Brokers.UpdateBroker;
using Senswave.DataSources.Api.Brokers.Clients.GetClientState;
using Senswave.DataSources.Api.Brokers.Clients.RestartClient;
using Senswave.DataSources.Api.Brokers.Clients.StartClient;
using Senswave.DataSources.Api.Brokers.Clients.StopClient;
using Senswave.DataSources.Api.Brokers.Sessions.GetSession;
using Senswave.DataSources.Api.Brokers.Sessions.GetSessions;
using Senswave.DataSources.Application.Brokers.Brokers.Features.GetBroker;
using Senswave.DataSources.Application.Brokers.Brokers.Features.GetBrokers;
using Senswave.DataSources.Application.Brokers.Brokers.Features.GetSubscribtions;
using Senswave.DataSources.Application.Brokers.Clients.ClientState;
using Senswave.DataSources.Application.Brokers.Sessions.GetSession;
using Senswave.DataSources.Application.Brokers.Sessions.GetSessions;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class DataSourcesGrpcService(IMediator mediator, IRequestContext requestContext)
    : Proto.DataSourcesService.DataSourcesServiceBase
{
    #region Brokers

    public override async Task<IdResponse> CreateBroker(Proto.CreateBrokerRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.UpdateBrokerResponse> UpdateBroker(Proto.UpdateBrokerRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateBrokerResponse();
    }

    public override async Task<Proto.DeleteBrokerResponse> DeleteBroker(Proto.DeleteBrokerRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.BrokerId, "brokerId").ToDeleteBrokerCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteBrokerResponse();
    }

    public override async Task<Proto.GetBrokerResponse> GetBroker(Proto.GetBrokerRequest request, ServerCallContext context)
    {
        var query = new GetBrokerQuery
        {
            UserId = requestContext.UserId,
            BrokerId = ProtoMapping.ParseGuid(request.BrokerId, "brokerId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.GetBrokersResponse> GetBrokers(Proto.GetBrokersRequest request, ServerCallContext context)
    {
        var query = new GetBrokersQuery
        {
            UserId = requestContext.UserId,
            Page = request.Page.OrDefault(1),
            Size = request.Size.OrDefault(10)
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    #endregion

    #region Subscriptions

    public override async Task<IdResponse> CreateSubscribtion(Proto.CreateSubscribtionRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.ToString() };
    }

    public override async Task<Proto.DeleteSubscribtionResponse> DeleteSubscribtion(Proto.DeleteSubscribtionRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteSubscribtionResponse();
    }

    public override async Task<Proto.GetSubscribtionsResponse> GetSubscribtions(Proto.GetSubscribtionsRequest request, ServerCallContext context)
    {
        var query = new GetSubscribtionsQuery
        {
            UserId = requestContext.UserId,
            BrokerId = ProtoMapping.ParseGuid(request.BrokerId, "brokerId"),
            Page = request.Page.OrDefault(1),
            Size = request.Size.OrDefault(10)
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    #endregion

    #region Clients

    public override async Task<Proto.GetClientStateResponse> GetClientState(Proto.GetClientStateRequest request, ServerCallContext context)
    {
        var query = new ClientStateQuery
        {
            BrokerId = ProtoMapping.ParseGuid(request.BrokerId, "brokerId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.StartClientResponse> StartClient(Proto.StartClientRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.StartClientResponse();
    }

    public override async Task<Proto.RestartClientResponse> RestartClient(Proto.RestartClientRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.BrokerId, "brokerId").ToRestartClientCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.RestartClientResponse();
    }

    public override async Task<Proto.StopClientResponse> StopClient(Proto.StopClientRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.BrokerId, "brokerId").ToStopClientCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.StopClientResponse();
    }

    #endregion

    #region Sessions

    public override async Task<Proto.GetSessionResponse> GetSession(Proto.GetSessionRequest request, ServerCallContext context)
    {
        var query = new GetSessionQuery
        {
            UserId = requestContext.UserId,
            BrokerId = ProtoMapping.ParseGuid(request.BrokerId, "brokerId"),
            SessionId = ProtoMapping.ParseGuid(request.SessionId, "sessionId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.GetSessionsResponse> GetSessions(Proto.GetSessionsRequest request, ServerCallContext context)
    {
        var query = new GetSessionsQuery
        {
            UserId = requestContext.UserId,
            BrokerId = ProtoMapping.ParseGuid(request.BrokerId, "brokerId"),
            Page = request.Page.OrDefault(1),
            Size = request.Size.OrDefault(5)
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    #endregion
}
