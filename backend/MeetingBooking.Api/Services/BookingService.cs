using System.Globalization;
using MeetingBooking.Api.Contracts.Bookings;
using MeetingBooking.Api.Models;
using MeetingBooking.Api.Repositories;

namespace MeetingBooking.Api.Services;

public class BookingService(
    IRoomRepository roomRepository,
    IBookingRepository bookingRepository,
    TimeProvider timeProvider) : IBookingService
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";
    private static readonly string[] AcceptedTimeFormats = [TimeFormat, "HH:mm:ss"];

    public async Task<CreateBookingResult> CreateBookingAsync(
        CreateBookingRequest request,
        string ownerKeyHash,
        CancellationToken cancellationToken)
    {
        if (!TryParseDetails(request, out var details, out var errors))
        {
            return new CreateBookingResult.Invalid(errors);
        }

        var room = await roomRepository.FindByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return new CreateBookingResult.RoomNotFound();
        }

        var booking = new Booking
        {
            RoomId = room.Id,
            GuestName = details.GuestName,
            MeetingTitle = details.MeetingTitle,
            Date = details.Date,
            StartTime = details.StartTime,
            EndTime = details.EndTime,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            OwnerKeyHash = ownerKeyHash,
        };

        var saved = await bookingRepository.TryAddIfSlotIsFreeAsync(booking, cancellationToken);
        return saved
            ? new CreateBookingResult.Created(ToResponse(booking, room.Name))
            : new CreateBookingResult.SlotTaken();
    }

    public async Task<BookingResponse?> GetBookingAsync(int bookingId, string ownerKeyHash, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindActiveForOwnerAsync(bookingId, ownerKeyHash, cancellationToken);
        return booking is null ? null : ToResponse(booking, booking.Room!.Name);
    }

    public async Task<UpdateBookingResult> UpdateBookingAsync(
        int bookingId,
        UpdateBookingRequest request,
        string ownerKeyHash,
        CancellationToken cancellationToken)
    {
        if (!TryParseDetails(request, out var details, out var errors))
        {
            return new UpdateBookingResult.Invalid(errors);
        }

        var (outcome, booking) = await bookingRepository.TryUpdateIfSlotIsFreeAsync(
            bookingId,
            ownerKeyHash,
            details,
            cancellationToken);

        return outcome switch
        {
            BookingUpdateOutcome.Updated => new UpdateBookingResult.Updated(ToResponse(booking!, booking!.Room!.Name)),
            BookingUpdateOutcome.SlotTaken => new UpdateBookingResult.SlotTaken(),
            _ => new UpdateBookingResult.NotFound(),
        };
    }

    public async Task<IReadOnlyList<BookingResponse>> GetUpcomingBookingsAsync(
        string ownerKeyHash,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow().DateTime;

        var bookings = await bookingRepository.GetActiveForOwnerEndingAfterAsync(
            ownerKeyHash,
            DateOnly.FromDateTime(now),
            TimeOnly.FromDateTime(now),
            cancellationToken);

        return bookings.Select(booking => ToResponse(booking, booking.Room!.Name)).ToList();
    }

    public Task<bool> CancelBookingAsync(int bookingId, string ownerKeyHash, CancellationToken cancellationToken) =>
        bookingRepository.CancelForOwnerAsync(
            bookingId,
            ownerKeyHash,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

    private static bool TryParseDetails(
        BookingDetailsRequest request,
        out BookingChanges details,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        details = null!;

        if (!DateOnly.TryParseExact(request.Date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors["date"] = ["Use the date format yyyy-MM-dd."];
        }

        var hasStart = TryParseTime(request.StartTime, out var startTime);
        var hasEnd = TryParseTime(request.EndTime, out var endTime);

        if (!hasStart)
        {
            errors["startTime"] = ["Use the time format HH:mm."];
        }

        if (!hasEnd)
        {
            errors["endTime"] = ["Use the time format HH:mm."];
        }
        else if (hasStart && endTime <= startTime)
        {
            errors["endTime"] = ["End time must be later than the start time."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        details = new BookingChanges(request.GuestName!.Trim(), request.MeetingTitle!.Trim(), date, startTime, endTime);
        return true;
    }

    private static BookingResponse ToResponse(Booking booking, string roomName) => new(
        booking.Id,
        booking.RoomId,
        roomName,
        booking.GuestName,
        booking.MeetingTitle,
        booking.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
        booking.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
        booking.EndTime.ToString(TimeFormat, CultureInfo.InvariantCulture));

    private static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, AcceptedTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
