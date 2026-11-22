using MeetingBooking.Api.Data;
using MeetingBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingBooking.Api.Repositories;

public class RoomRepository(BookingDbContext dbContext) : IRoomRepository
{
    public async Task<IReadOnlyList<Room>> GetAllOrderedByNameAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Rooms
            .AsNoTracking()
            .OrderBy(room => room.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Room?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == id, cancellationToken);
    }
}
