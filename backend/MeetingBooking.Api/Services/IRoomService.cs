using MeetingBooking.Api.Contracts.Rooms;

namespace MeetingBooking.Api.Services;

public interface IRoomService
{
    Task<IReadOnlyList<RoomResponse>> GetRoomsAsync(CancellationToken cancellationToken);
}
