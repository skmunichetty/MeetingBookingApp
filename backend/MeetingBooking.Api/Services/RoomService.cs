using MeetingBooking.Api.Contracts.Rooms;
using MeetingBooking.Api.Repositories;

namespace MeetingBooking.Api.Services;

public class RoomService(IRoomRepository roomRepository) : IRoomService
{
    public async Task<IReadOnlyList<RoomResponse>> GetRoomsAsync(CancellationToken cancellationToken)
    {
        var rooms = await roomRepository.GetAllOrderedByNameAsync(cancellationToken);

        return rooms
            .Select(room => new RoomResponse(room.Id, room.Name, room.Capacity))
            .ToList();
    }
}
