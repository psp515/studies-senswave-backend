using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.DataSources.Domain.Brokers.Brokers.Enums;
using Senswave.DataSources.Domain.Brokers.Brokers.Options;
using Senswave.DataSources.Domain.Brokers.Brokers.Repositories;
using Senswave.DataSources.Domain.Brokers.Brokers.Services;
using Senswave.DataSources.Domain.Brokers.Clients.Proxy;

namespace Senswave.DataSources.Application.Brokers.Brokers.Features.UpdateBroker;

public class UpdateBrokerHandler(
    IBrokerService brokerService,
    IClientProxy clienProxy,
    IBrokerQueryRepository queryRepository,
    IBrokerCommandRepository commandRepository,
    IOptions<BrokerOptions> options,
    ILogger<UpdateBrokerHandler> logger) : ICommandHandler<UpdateBrokerCommand>
{
    public async Task<Result> Handle(UpdateBrokerCommand request, CancellationToken cancellationToken)
    {
        var broker = await commandRepository.GetBroker(request.BrokerId, request.UserId, cancellationToken);

        if (broker is null)
        {
            logger.LogWarning("[User: {userId}] Broker with id = {brokerId} not found.", request.UserId, request.BrokerId);
            return Result.Failure(UpdateBrokerErrors.NotFound);
        }

        var exists = await clienProxy.ClientExists(request.BrokerId, cancellationToken);

        if (exists.IsSuccess)
        {
            logger.LogWarning("[User: {userId}] [Broker: {brokerId}] Broker has an active client.", request.UserId, request.BrokerId);
            return Result<Broker>.Failure(UpdateBrokerErrors.ClientExists);
        }

        bool testConnection = false;

        if (request.Port >= 1 && request.Port <= 65535 || !string.IsNullOrEmpty(request.Url))
        {
            var port = request.Port >= 1 && request.Port <= 65535 ? request.Port : broker.BrokerInfo.Port;
            var url = !string.IsNullOrEmpty(request.Url) ? request.Url : broker.BrokerInfo.Url;

            var brokerExists = !options.Value.AllowSharedBrokers
                && await queryRepository.BrokerExists(url, port, cancellationToken);

            if (brokerExists)
            {
                logger.LogWarning("[User: {userId}] Broker with URL '{url}' and Port '{port}' already used by someone.", request.UserId, url, port);
                return Result<Broker>.Failure(UpdateBrokerErrors.BrokerAlreadyUsed);
            }

            if (request.Port > 0)
                broker.BrokerInfo.Port = request.Port;

            if (!string.IsNullOrEmpty(request.Url))
                broker.BrokerInfo.Url = request.Url;

            testConnection = true;
        }

        if (!string.IsNullOrEmpty(request.ClientName))
        {
            var clientUsed = await queryRepository.ClientNameIsUsedForBroker(request.ClientName, broker.BrokerInfo.Url, broker.BrokerInfo.Port, cancellationToken);

            if (clientUsed)
            {
                logger.LogWarning("[User: {userId}] Client name '{clientName}' already used for broker with URL '{url}' and Port '{port}'.",
                    request.UserId, request.ClientName, broker.BrokerInfo.Url, broker.BrokerInfo.Port);
                return Result<Broker>.Failure(UpdateBrokerErrors.ClientNameUsed);
            }

            if (!clientUsed)
                broker.BrokerInfo.ClientName = request.ClientName;

            testConnection = true;
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            var nameUsed = await queryRepository.BrokerNameUsedByUser(request.Name, request.UserId, cancellationToken);

            if (nameUsed)
            {
                logger.LogWarning("[User: {userId}] Broker name '{brokerName}' already used.", request.UserId, request.Name);
                return Result<Broker>.Failure(UpdateBrokerErrors.BrokerNameUsed);
            }

            broker.Name = request.Name;
        }

        if (request.ProtocolVersion != BrokerProtocolVersion.Empty && request.ProtocolVersion != BrokerProtocolVersion.Invalid)
        {
            broker.BrokerInfo.ProtocolVersion = request.ProtocolVersion;
            testConnection = true;
        }

        if (request.UseTls is not null)
        {
            broker.BrokerInfo.UseTls = request.UseTls.Value;
            testConnection = true;
        }

        if (options.Value.TestConnectionOnChange && testConnection)
        {
            var connectionTest = await brokerService.TestConnection(
                broker.BrokerInfo.Url,
                broker.BrokerInfo.Port,
                broker.BrokerInfo.ClientName,
                broker.BrokerInfo.UseTls,
                broker.BrokerInfo.ProtocolVersion,
                request.Username,
                request.Password,
                cancellationToken);

            if (connectionTest.IsFailure)
            {
                logger.LogWarning("[User: {userId}] Broker connection test failed.", request.UserId);
                return Result<Broker>.Failure(connectionTest.Errors);
            }
        }

        var result = await commandRepository.UpdateBroker(broker, cancellationToken);

        if (!result)
        {
            logger.LogError("[User: {userId}] [Broker: {brokerId}] Failed to update broker.", request.UserId, request.BrokerId);
            return Result.Failure(UpdateBrokerErrors.UpdateFailed, result.Errors);
        }

        logger.LogInformation("[User: {userId}] [Broker: {brokerId}] Broker sucessfully updated.", request.UserId, broker.Id);
        return Result.Success();
    }
}
