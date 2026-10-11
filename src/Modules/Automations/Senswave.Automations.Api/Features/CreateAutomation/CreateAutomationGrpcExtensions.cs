using Senswave.Automations.Application.Features.CreateAutomation;
using Senswave.Automations.Domain.Entities;
using Senswave.Automations.Domain.Enums;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.Api.Features.CreateAutomation;

/// <summary>
/// Proto mapping for automations. The enum conversions are shared with Update, PutCondition and Get/Display.
/// </summary>
internal static class CreateAutomationGrpcExtensions
{
    internal static CreateAutomationCommand ToCommand(this Proto.CreateAutomationRequest dto, Guid userId) => new()
    {
        UserId = userId,
        HomeId = ProtoMapping.ParseGuid(dto.HomeId, "homeId"),
        Name = dto.Name,
        Icon = dto.Icon,
        ConditionConnector = dto.ConditionConnector.ToDomain(),
        Conditions = dto.Conditions.Select(x => x.ToDomain()).ToList(),
        Results = dto.Results.Select(x => x.ToDomain()).ToList()
    };

    internal static AutomationCondition ToDomain(this Proto.ConditionDto dto) => new()
    {
        OperationId = ProtoMapping.ParseGuid(dto.OperationId, "operationId"),
        ConditionConfiguration = dto.ConditionConfiguration.ToJsonObject("conditionConfiguration"),
        ConditionType = dto.ConditionType.ToDomain()
    };

    internal static AutomationResult ToDomain(this Proto.ResultDto dto) => new()
    {
        OperationId = ProtoMapping.ParseGuid(dto.OperationId, "operationId"),
        ValueToSend = dto.ValueToSend.ToJsonValue("valueToSend")
    };

    /// <summary>Unspecified mirrors an omitted REST field (empty string → <c>Empty</c>).</summary>
    internal static AutomationConditionConnector ToDomain(this Proto.ConditionConnector connector) => connector switch
    {
        Proto.ConditionConnector.Unspecified => AutomationConditionConnector.Empty,
        Proto.ConditionConnector.And => AutomationConditionConnector.And,
        Proto.ConditionConnector.Or => AutomationConditionConnector.Or,
        _ => AutomationConditionConnector.Invalid
    };

    internal static AutomationConditionType ToDomain(this Proto.ConditionType type) => type switch
    {
        Proto.ConditionType.Unspecified => AutomationConditionType.Empty,
        Proto.ConditionType.Boolean => AutomationConditionType.BooleanCondition,
        Proto.ConditionType.Number => AutomationConditionType.NumberCondition,
        Proto.ConditionType.Text => AutomationConditionType.TextCondition,
        _ => AutomationConditionType.Invalid
    };

    /// <summary>Application models carry the domain enum name (<c>Enum.ToString()</c>).</summary>
    internal static Proto.ConditionConnector ToProtoConditionConnector(this string connector) => connector switch
    {
        nameof(AutomationConditionConnector.And) => Proto.ConditionConnector.And,
        nameof(AutomationConditionConnector.Or) => Proto.ConditionConnector.Or,
        _ => Proto.ConditionConnector.Unspecified
    };

    internal static Proto.ConditionType ToProtoConditionType(this string type) => type switch
    {
        nameof(AutomationConditionType.BooleanCondition) => Proto.ConditionType.Boolean,
        nameof(AutomationConditionType.NumberCondition) => Proto.ConditionType.Number,
        nameof(AutomationConditionType.TextCondition) => Proto.ConditionType.Text,
        _ => Proto.ConditionType.Unspecified
    };
}
