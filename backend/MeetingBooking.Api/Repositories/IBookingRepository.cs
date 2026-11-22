using MeetingBooking.Api.Models;

namespace MeetingBooking.Api.Repositories;

public sealed record BookingChanges(string GuestName, string MeetingTitle, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

public enum BookingUpdateOutcome
{
    Updated,
    NotFound,
    SlotTaken,
}

public interface IBookingRepository
{
    Task<bool> TryAddIfSlotIsFreeAsync(Booking booking, CancellationToken cancellationToken);

    Task<Booking?> FindActiveForOwnerAsync(int bookingId, string ownerKeyHash, CancellationToken cancellationToken);

    Task<(BookingUpdateOutcome Outcome, Booking? Booking)> TryUpdateIfSlotIsFreeAsync(
        int bookingId,
        string ownerKeyHash,
        BookingChanges changes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Booking>> GetActiveForOwnerEndingAfterAsync(
        string ownerKeyHash,
        DateOnly date,
        TimeOnly time,
        CancellationToken cancellationToken);

    Task<bool> CancelForOwnerAsync(int bookingId, string ownerKeyHash, DateTime cancelledAtUtc, CancellationToken cancellationToken);
}
