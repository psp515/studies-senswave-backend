using Google.Protobuf.WellKnownTypes;
using Senswave.Users.Domain.Entity;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Legal.GetPrivacyPolicy;

internal static class GetPrivacyPolicyGrpcExtensions
{
    public static Proto.GetPrivacyPolicyResponse ToProto(this PrivacyPolicy privacyPolicy) => new()
    {
        Id = privacyPolicy.Id.ToString(),
        Version = privacyPolicy.Version,
        Summary = privacyPolicy.Summary,
        Content = privacyPolicy.Content,
        CreatedAtUtc = DateTime.SpecifyKind(privacyPolicy.CreatedAtUtc, DateTimeKind.Utc).ToTimestamp(),
    };
}
