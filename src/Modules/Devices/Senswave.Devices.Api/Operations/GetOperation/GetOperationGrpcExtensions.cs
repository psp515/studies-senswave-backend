using Senswave.Devices.Application.Operations.Features.GetOperation;
using Senswave.Devices.Domain.Operations.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Operations.GetOperation;

internal static class GetOperationGrpcExtensions
{
    internal static Proto.GetOperationResponse ToProto(this ExtendedOperationModel model) => new()
    {
        Id = model.Operation.Id.ToString(),
        Name = model.Operation.Name,
        Configuration = model.Operation.Configuration.ToStruct(),
        Type = model.Operation.Type.FromOperationType(),
        Topic = model.Topic
    };
}
