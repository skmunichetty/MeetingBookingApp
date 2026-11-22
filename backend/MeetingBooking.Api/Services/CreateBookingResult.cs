using MeetingBooking.Api.Contracts.Bookings;

namespace MeetingBooking.Api.Services;

public abstract record CreateBookingResult
{
    private CreateBookingResult()
    {
    }

    public sealed record Created(BookingResponse Booking) : CreateBookingResult;

    public sealed record Invalid(IDictionary<string, string[]> Errors) : CreateBookingResult;

    public sealed record RoomNotFound : CreateBookingResult;

    public sealed record SlotTaken : CreateBookingResult;
}
