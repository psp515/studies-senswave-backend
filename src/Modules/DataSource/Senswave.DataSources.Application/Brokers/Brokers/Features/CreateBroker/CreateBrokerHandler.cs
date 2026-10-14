using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.DataSources.Domain.Brokers.Brokers.Options;
using Senswave.DataSources.Domain.Brokers.Brokers.Repositories;
using Senswave.DataSources.Domain.Brokers.Brokers.Services;
using Senswave.DataSources.Domain.Brokers.Brokers.ValueObjects;

namespace Senswave.DataSources.Application.Brokers.Brokers.Features.CreateBroker;

public class PostBrokerConnectionHandler(
    IBrokerService brokerService,
    IBrokerQueryRepository queryRepository,
    IBrokerCommandRepository commandRepository,
    IOptions<BrokerOptions> options,
    ILogger<PostBrokerConnectionHandler> logger) : ICommandHandler<CreateBrokerCommand, Broker>
{
    public async Task<Result<Broker>> Handle(CreateBrokerCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await queryRepository.BrokerNameUsedByUser(request.Name, request.UserId, cancellationToken);

        if (nameExists)
        {
            logger.LogWarning("[User: {userId}] Broker name '{brokerName}' already used.",
                request.UserId, request.Name);
            return Result<Broker>.Failure(CreateBrokerErrors.NameAlreadyUsed);
        }

        var brokerExists = !options.Value.AllowSharedBrokers
            && await queryRepository.BrokerExists(request.Url, request.Port, cancellationToken);

        if (brokerExists)
        {
            logger.LogWarning("[User: {userId}] Broker with URL '{url}' and Port '{port}' already used by someone.",
                request.UserId, request.Url, request.Port);
            return Result<Broker>.Failure(CreateBrokerErrors.BrokerAlreadyUsed);
        }

        var clientNameIsUsed = await queryRepository.ClientNameIsUsedForBroker(request.ClientName, request.Url, request.Port, cancellationToken);

        if (clientNameIsUsed)
        {
            logger.LogWarning("[User: {userId}] Client name '{clientName}' already used for broker with URL '{url}' and Port '{port}'.",
                request.UserId, request.ClientName, request.Url, request.Port);
            return Result<Broker>.Failure(CreateBrokerErrors.ClientNameUsed);
        }

        //TODO: Redis Lock per User

        var currentBrokers = await queryRepository.CountBrokersForUser(request.UserId, cancellationToken);

        if (options.Value.Limits.Brokers <= currentBrokers)
        {
            logger.LogWarning("[User: {userId}] Maximum number of brokers reached: {maxBrokers}.",
                request.UserId, options.Value.Limits.Brokers);
            return Result<Broker>.Failure(CreateBrokerErrors.MaximalNumberOfBrokers);
        }

        var globalBrokersCount = await queryRepository.CountGlobalBrokers(cancellationToken);

        if (options.Value.Limits.InstanceMaxBrokerClients <= globalBrokersCount)
        {
            logger.LogWarning("[User: {userId}] Maximum number of global brokers reached: {maxGlobalBrokers}.",
                request.UserId, options.Value.Limits.InstanceMaxBrokerClients);
            return Result<Broker>.Failure(CreateBrokerErrors.MaximalNumberOfGlobalBrokers);
        }

        if (options.Value.TestConnectionOnChange)
        {
            var connectionTest = await brokerService.TestConnection(
                request.Url,
                request.Port,
                request.ClientName,
                request.UseTls,
                request.ProtocolVersion,
                request.Username,
                request.Password,
                cancellationToken);

            if (connectionTest.IsFailure)
            {
                logger.LogWarning("[User: {userId}] Broker connection test failed.", request.UserId);
                return Result<Broker>.Failure(connectionTest.Errors);
            }
        }

        var broker = new Broker
        {
            OwnerId = request.UserId,
            Name = request.Name,

            BrokerInfo = new BrokerInfo
            {
                Url = request.Url,
                ClientName = request.ClientName,
                Port = request.Port,
                ProtocolVersion = request.ProtocolVersion,
                UseTls = request.UseTls,
            },
        };

        var result = await commandRepository.CreateBroker(broker, cancellationToken);

        if (result.IsFailure)
        {
            logger.LogError("[User: {userId}] Failed to create broker.", request.UserId);
            return Result<Broker>.Failure(CreateBrokerErrors.CreationFailed, result.Errors);
        }

        logger.LogInformation("[Broker: {brokerId}] Broker sucessfully created.", broker.Id);

        return Result<Broker>.Success(broker);
    }
}
