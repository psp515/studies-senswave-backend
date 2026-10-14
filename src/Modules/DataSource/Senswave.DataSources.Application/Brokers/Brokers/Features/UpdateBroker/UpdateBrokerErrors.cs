namespace Senswave.DataSources.Application.Brokers.Brokers.Features.UpdateBroker;

public class UpdateBrokerErrors
{
    public static Error UpdateFailed
        => Error.ServerError("UpdateFailed", "Update failed.");
    public static Error NotFound
        => Error.NotFound("BrokerNotFound", "Broker not found");
    public static Error ClientExists
        => Error.Failure("ClientExists", "Client exists for this broker. First disconnect client.");
    public static Error BrokerNameUsed
        => Error.Conflict("BrokerNameUsed", "Name already used by other broker.");
    public static Error BrokerAlreadyUsed
        => Error.Conflict("BrokerAlreadyUsed", "Broker is already used.");
    public static Error ClientNameUsed
        => Error.Conflict("ClientNameUsed", "Client name is used for this broker, couldn't add new connection due to possible steal session problem.");
}
