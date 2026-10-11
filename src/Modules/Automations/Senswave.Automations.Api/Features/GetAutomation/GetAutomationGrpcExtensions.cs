using Senswave.Automations.Api.Features.CreateAutomation;
using Senswave.Automations.Application.Models;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.Api.Features.GetAutomation;

internal static class GetAutomationGrpcExtensions
{
    internal static Proto.AutomationModel ToProto(this AutomationModel model) => new()
    {
        Id = model.Id.ToString(),
        HomeId = model.HomeId.ToString(),
        Name = model.Name,
        Icon = model.Icon,
        ConditionConnector = model.ConditionConnector.ToProtoConditionConnector(),
        IsEnabled = model.IsEnabled,
        Conditions =
        {
            model.Conditions.Select(condition => new Proto.ConditionModel
            {
                Id = condition.Id.ToString(),
                OperationId = condition.OperationId.ToString(),
                OperationName = condition.OperationName,
                ConditionType = condition.ConditionType.ToProtoConditionType(),
                ConditionConfiguration = condition.ConditionConfiguration.ToStruct()
            })
        },
        Results =
        {
            model.Results.Select(result => new Proto.ResultModel
            {
                Id = result.Id.ToString(),
                OperationId = result.OperationId.ToString(),
                OperationName = result.OperationName,
                ValueToSend = result.ValueToSend.ToProtoValue()
            })
        }
    };
}
