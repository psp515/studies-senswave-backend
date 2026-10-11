using Senswave.Devices.Domain.Operations.Entities;
using Senswave.Devices.Domain.Operations.Extensions;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Operations.DisplayOperations;

internal static class DisplayOperationsGrpcExtensions
{
    internal static Proto.DisplayOperationsResponse ToProto(this IEnumerable<Operation> operations) => new()
    {
        Items =
        {
            operations.Select(operation => new Proto.OperationDto
            {
                Id = operation.Id.ToString(),
                Name = operation.Name,
                Type = operation.Type.FromOperationType()
            })
        }
    };
}
