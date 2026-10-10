using Grpc.Core;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Users.Api.Users.GetUser;
using Senswave.Users.Api.Users.UpdateSettings;
using Senswave.Users.Application.Users.CreateConsents;
using Senswave.Users.Application.Users.DeleteAccount;
using Senswave.Users.Application.Users.GetUser;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class UsersGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.UsersService.UsersServiceBase
{
    public override async Task<Proto.GetUserResponse> GetUser(Proto.GetUserRequest request, ServerCallContext context)
    {
        var query = new GetUserQuery
        {
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.UpdateSettingsResponse> UpdateSettings(Proto.UpdateSettingsRequest request, ServerCallContext context)
    {
        var command = request.ToCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateSettingsResponse();
    }

    public override async Task<Proto.CreateConsentsResponse> CreateConsents(Proto.CreateConsentsRequest request, ServerCallContext context)
    {
        var command = new CreateConsentsCommand
        {
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.CreateConsentsResponse();
    }

    public override async Task<Proto.DeleteAccountResponse> DeleteAccount(Proto.DeleteAccountRequest request, ServerCallContext context)
    {
        var command = new DeleteAccountCommand
        {
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteAccountResponse();
    }
}
