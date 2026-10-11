using Senswave.DataSources.Application.Brokers.Clients.ClientState;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Clients.GetClientState;

internal static class GetClientStateGrpcExtensions
{
    internal static Proto.GetClientStateResponse ToProto(this ClientStateModel model) => new()
    {
        ConnectionStatus = model.State.ToString(),
        LatestSessionId = model.LatestSession.ToString(),
    };
}
