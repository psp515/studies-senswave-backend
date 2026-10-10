using Grpc.Core;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Users.Api.Legal.GetPrivacyPolicy;
using Senswave.Users.Api.Legal.GetTermsAndConditions;
using Senswave.Users.Domain.Interfaces;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Grpc;

[AllowAnonymous]
internal sealed class LegalGrpcService(ILegalService legalService) : Proto.LegalService.LegalServiceBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public override async Task<Proto.GetPrivacyPolicyResponse> GetPrivacyPolicy(Proto.GetPrivacyPolicyRequest request, ServerCallContext context)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(Timeout);

        var privacyPolicy = await legalService.GetPrivacyPolicy(cts.Token);
        privacyPolicy.ThrowIfFailure();

        return privacyPolicy.Data.ToProto();
    }

    public override async Task<Proto.GetTermsAndConditionsResponse> GetTermsAndConditions(Proto.GetTermsAndConditionsRequest request, ServerCallContext context)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(Timeout);

        var terms = await legalService.GetTermsAndConditions(cts.Token);
        terms.ThrowIfFailure();

        return terms.Data.ToProto();
    }
}
