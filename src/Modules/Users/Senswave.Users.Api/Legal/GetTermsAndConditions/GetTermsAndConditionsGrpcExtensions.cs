using Google.Protobuf.WellKnownTypes;
using Senswave.Users.Domain.Entity;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Legal.GetTermsAndConditions;

internal static class GetTermsAndConditionsGrpcExtensions
{
    public static Proto.GetTermsAndConditionsResponse ToProto(this TermsAndConditions terms) => new()
    {
        Id = terms.Id.ToString(),
        Version = terms.Version,
        Summary = terms.Summary,
        Content = terms.Content,
        CreatedAtUtc = DateTime.SpecifyKind(terms.CreatedAtUtc, DateTimeKind.Utc).ToTimestamp(),
    };
}
