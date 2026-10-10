using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.Homes.Api.Homes.Features.CreateHome;
using Senswave.Homes.Api.Homes.Features.DeleteHome;
using Senswave.Homes.Api.Homes.Features.DeleteHomeDataSource;
using Senswave.Homes.Api.Homes.Features.GetHome;
using Senswave.Homes.Api.Homes.Features.GetHomes;
using Senswave.Homes.Api.Homes.Features.SetHomeDataSource;
using Senswave.Homes.Api.Homes.Features.UpdateHome;
using Senswave.Homes.Application.Homes.Features.GetCurrentHome;
using Senswave.Homes.Application.Homes.Features.GetHome;
using Senswave.Homes.Application.Homes.Features.GetHomes;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class HomesGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.HomesService.HomesServiceBase
{
    public override async Task<IdResponse> CreateHome(Proto.CreateHomeRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.UpdateHomeResponse> UpdateHome(Proto.UpdateHomeRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateHomeResponse();
    }

    public override async Task<Proto.DeleteHomeResponse> DeleteHome(Proto.DeleteHomeRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.HomeId, "homeId").ToDeleteHomeCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteHomeResponse();
    }

    public override async Task<Proto.GetHomeResponse> GetHome(Proto.GetHomeRequest request, ServerCallContext context)
    {
        var query = new GetHomeQuery
        {
            UserId = requestContext.UserId,
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto(requestContext.UserId);
    }

    public override async Task<Proto.GetHomesResponse> GetHomes(Proto.GetHomesRequest request, ServerCallContext context)
    {
        var query = new GetHomesQuery
        {
            Page = request.Page.OrDefault(1),
            Size = request.Size.OrDefault(10),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto(requestContext.UserId);
    }

    public override async Task<IdResponse> GetCurrentHome(Proto.GetCurrentHomeRequest request, ServerCallContext context)
    {
        var query = new GetCurrentHomeQuery
        {
            UserId = requestContext.UserId,
            Latitude = request.HasLatitude ? request.Latitude : null,
            Longitude = request.HasLongitude ? request.Longitude : null
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.SetHomeDataSourceResponse> SetHomeDataSource(Proto.SetHomeDataSourceRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.SetHomeDataSourceResponse();
    }

    public override async Task<Proto.DeleteHomeDataSourceResponse> DeleteHomeDataSource(Proto.DeleteHomeDataSourceRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.HomeId, "homeId").ToDeleteHomeDataSourceCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteHomeDataSourceResponse();
    }
}
