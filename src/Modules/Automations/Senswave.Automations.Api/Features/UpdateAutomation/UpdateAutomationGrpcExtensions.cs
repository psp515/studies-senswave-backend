using Senswave.Automations.Api.Features.CreateAutomation;
using Senswave.Automations.Application.Features.UpdateAutomation;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.Api.Features.UpdateAutomation;

internal static class UpdateAutomationGrpcExtensions
{
    internal static UpdateAutomationCommand ToCommand(this Proto.UpdateAutomationRequest dto, Guid userId) => new()
    {
        UserId = userId,
        AutomationId = ProtoMapping.ParseGuid(dto.AutomationId, "automationId"),
        Name = dto.Name,
        Icon = dto.Icon,
        ConditionConnector = dto.ConditionConnector.ToDomain(),
        Conditions = dto.Conditions.Select(x => x.ToDomain()).ToList(),
        Results = dto.Results.Select(x => x.ToDomain()).ToList()
    };
}
