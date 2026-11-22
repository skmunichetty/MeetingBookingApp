namespace MeetingBooking.Api.Models;

public class Booking
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public Room? Room { get; set; }

    public required string GuestName { get; set; }

    public required string MeetingTitle { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? OwnerKeyHash { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
