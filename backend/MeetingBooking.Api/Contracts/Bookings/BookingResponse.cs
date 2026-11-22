namespace MeetingBooking.Api.Contracts.Bookings;

public sealed record BookingResponse(
    int Id,
    int RoomId,
    string RoomName,
    string GuestName,
    string MeetingTitle,
    string Date,
    string StartTime,
    string EndTime);
