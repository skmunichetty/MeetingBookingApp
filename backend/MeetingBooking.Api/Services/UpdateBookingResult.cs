using MeetingBooking.Api.Contracts.Bookings;

namespace MeetingBooking.Api.Services;

public abstract record UpdateBookingResult
{
    private UpdateBookingResult()
    {
    }

    public sealed record Updated(BookingResponse Booking) : UpdateBookingResult;

    public sealed record Invalid(IDictionary<string, string[]> Errors) : UpdateBookingResult;

    public sealed record NotFound : UpdateBookingResult;

    public sealed record SlotTaken : UpdateBookingResult;
}
