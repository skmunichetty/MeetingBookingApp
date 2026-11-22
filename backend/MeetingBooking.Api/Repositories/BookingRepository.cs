using MeetingBooking.Api.Data;
using MeetingBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingBooking.Api.Repositories;

public class BookingRepository(BookingDbContext dbContext) : IBookingRepository
{
    public async Task<bool> TryAddIfSlotIsFreeAsync(Booking booking, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (await OverlapsAnotherActiveBookingAsync(booking.RoomId, booking.Date, booking.StartTime, booking.EndTime, excludeBookingId: null, cancellationToken))
        {
            return false;
        }

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<Booking?> FindActiveForOwnerAsync(int bookingId, string ownerKeyHash, CancellationToken cancellationToken)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .FirstOrDefaultAsync(
                booking => booking.Id == bookingId
                    && booking.OwnerKeyHash == ownerKeyHash
                    && booking.CancelledAtUtc == null,
                cancellationToken);
    }

    public async Task<(BookingUpdateOutcome Outcome, Booking? Booking)> TryUpdateIfSlotIsFreeAsync(
        int bookingId,
        string ownerKeyHash,
        BookingChanges changes,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await dbContext.Bookings
            .Include(b => b.Room)
            .FirstOrDefaultAsync(
                b => b.Id == bookingId && b.OwnerKeyHash == ownerKeyHash && b.CancelledAtUtc == null,
                cancellationToken);

        if (booking is null)
        {
            return (BookingUpdateOutcome.NotFound, null);
        }

        if (await OverlapsAnotherActiveBookingAsync(booking.RoomId, changes.Date, changes.StartTime, changes.EndTime, excludeBookingId: booking.Id, cancellationToken))
        {
            return (BookingUpdateOutcome.SlotTaken, null);
        }

        booking.GuestName = changes.GuestName;
        booking.MeetingTitle = changes.MeetingTitle;
        booking.Date = changes.Date;
        booking.StartTime = changes.StartTime;
        booking.EndTime = changes.EndTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (BookingUpdateOutcome.Updated, booking);
    }

    private Task<bool> OverlapsAnotherActiveBookingAsync(
        int roomId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeBookingId,
        CancellationToken cancellationToken) =>
        dbContext.Bookings.AnyAsync(
            existing => existing.RoomId == roomId
                && existing.CancelledAtUtc == null
                && existing.Id != excludeBookingId
                && existing.Date == date
                && existing.StartTime < endTime
                && startTime < existing.EndTime,
            cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetActiveForOwnerEndingAfterAsync(
        string ownerKeyHash,
        DateOnly date,
        TimeOnly time,
        CancellationToken cancellationToken)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .Where(booking => booking.OwnerKeyHash == ownerKeyHash
                && booking.CancelledAtUtc == null
                && (booking.Date > date || (booking.Date == date && booking.EndTime > time)))
            .OrderBy(booking => booking.Date)
            .ThenBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CancelForOwnerAsync(
        int bookingId,
        string ownerKeyHash,
        DateTime cancelledAtUtc,
        CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.FirstOrDefaultAsync(
            b => b.Id == bookingId && b.OwnerKeyHash == ownerKeyHash,
            cancellationToken);

        if (booking is null)
        {
            return false;
        }

        if (booking.CancelledAtUtc is null)
        {
            booking.CancelledAtUtc = cancelledAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
