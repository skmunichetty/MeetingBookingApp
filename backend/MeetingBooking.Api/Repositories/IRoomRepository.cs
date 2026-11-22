using MeetingBooking.Api.Models;

namespace MeetingBooking.Api.Repositories;

public interface IRoomRepository
{
    Task<IReadOnlyList<Room>> GetAllOrderedByNameAsync(CancellationToken cancellationToken);

    Task<Room?> FindByIdAsync(int id, CancellationToken cancellationToken);
}
