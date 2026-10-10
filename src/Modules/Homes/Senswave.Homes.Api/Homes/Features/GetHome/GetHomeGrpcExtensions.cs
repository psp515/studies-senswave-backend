using Senswave.Homes.Api.Homes.Shared;
using Senswave.Homes.Application.Homes.Models;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Homes.Features.GetHome;

internal static class GetHomeGrpcExtensions
{
    internal static Proto.GetHomeResponse ToProto(this GetHomeModel model, Guid userId)
    {
        var response = new Proto.GetHomeResponse
        {
            Id = model.Home.Id.ToString(),
            Name = model.Home.Name,
            Icon = model.Home.Icon,
            IsOwner = model.Home.OwnerId == userId,
            Location = model.Home.Location.ToProto(),
        };

        if (model.Home.DataSourceReference is not null)
        {
            response.DataSource = new Proto.DataSourceDto
            {
                Id = model.Home.DataSourceReference.DataSourceId.ToString(),
                State = model.DataSource!.State,
                Name = model.DataSource!.Name
            };
        }

        response.Rooms.AddRange(model.Home.Rooms.Select(room => new Proto.HomeRoomDto
        {
            Id = room.Id.ToString(),
            Name = room.Name
        }));

        return response;
    }
}
