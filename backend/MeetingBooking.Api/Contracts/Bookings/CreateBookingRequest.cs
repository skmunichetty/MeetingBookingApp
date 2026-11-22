using System.ComponentModel.DataAnnotations;
using MeetingBooking.Api.Data;

namespace MeetingBooking.Api.Contracts.Bookings;

public abstract record BookingDetailsRequest
{
    [Required(ErrorMessage = "Enter your name.")]
    [MaxLength(BookingDbContext.GuestNameMaxLength)]
    public string? GuestName { get; init; }

    [Required(ErrorMessage = "Enter a meeting title.")]
    [MaxLength(BookingDbContext.MeetingTitleMaxLength)]
    public string? MeetingTitle { get; init; }

    [Required(ErrorMessage = "Choose a date.")]
    public string? Date { get; init; }

    [Required(ErrorMessage = "Choose a start time.")]
    public string? StartTime { get; init; }

    [Required(ErrorMessage = "Choose an end time.")]
    public string? EndTime { get; init; }
}

public sealed record CreateBookingRequest : BookingDetailsRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a room.")]
    public int RoomId { get; init; }
}

public sealed record UpdateBookingRequest : BookingDetailsRequest;
