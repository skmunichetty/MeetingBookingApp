using MeetingBooking.Api.Contracts.Bookings;

namespace MeetingBooking.Api.Services;

public interface IBookingService
{
    Task<CreateBookingResult> CreateBookingAsync(
        CreateBookingRequest request,
        string ownerKeyHash,
        CancellationToken cancellationToken);

    Task<BookingResponse?> GetBookingAsync(int bookingId, string ownerKeyHash, CancellationToken cancellationToken);

    Task<UpdateBookingResult> UpdateBookingAsync(
        int bookingId,
        UpdateBookingRequest request,
        string ownerKeyHash,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingResponse>> GetUpcomingBookingsAsync(string ownerKeyHash, CancellationToken cancellationToken);

    Task<bool> CancelBookingAsync(int bookingId, string ownerKeyHash, CancellationToken cancellationToken);
}
