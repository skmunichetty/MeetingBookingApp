using MeetingBooking.Api.Contracts.Bookings;
using MeetingBooking.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeetingBooking.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController(
    IBookingService bookingService,
    IGuestSessionService guestSessionService,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBooking(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (guestSessionService.GetOwnerKey(HttpContext) is not { } ownerKey)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Guest session required.",
                detail: "Reload the page, then try booking again.");
        }

        var result = await bookingService.CreateBookingAsync(request, ownerKey, cancellationToken);

        return result switch
        {
            CreateBookingResult.Created created => StatusCode(StatusCodes.Status201Created, created.Booking),
            CreateBookingResult.Invalid invalid => ValidationProblem(new ValidationProblemDetails(invalid.Errors)),
            CreateBookingResult.RoomNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Room not found.",
                detail: $"There is no room with ID {request.RoomId}."),
            CreateBookingResult.SlotTaken => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That time is already booked.",
                detail: "Another booking for this room overlaps the requested time. Choose another time."),
            _ => throw new InvalidOperationException($"Unhandled booking result {result.GetType().Name}."),
        };
    }

    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<BookingResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> GetMyBookings(CancellationToken cancellationToken)
    {
        var ownerKey = guestSessionService.EnsureSession(HttpContext);
        return Ok(await bookingService.GetUpcomingBookingsAsync(ownerKey, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> GetBooking(int id, CancellationToken cancellationToken)
    {
        var ownerKey = guestSessionService.EnsureSession(HttpContext);
        var booking = await bookingService.GetBookingAsync(id, ownerKey, cancellationToken);
        return booking is null ? BookingNotFound() : Ok(booking);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBooking(int id, UpdateBookingRequest request, CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return AntiforgeryFailed();
        }

        if (guestSessionService.GetOwnerKey(HttpContext) is not { } ownerKey)
        {
            return BookingNotFound();
        }

        var result = await bookingService.UpdateBookingAsync(id, request, ownerKey, cancellationToken);

        return result switch
        {
            UpdateBookingResult.Updated updated => Ok(updated.Booking),
            UpdateBookingResult.Invalid invalid => ValidationProblem(new ValidationProblemDetails(invalid.Errors)),
            UpdateBookingResult.NotFound => BookingNotFound(),
            UpdateBookingResult.SlotTaken => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That time is already booked.",
                detail: "Another booking for this room overlaps the new time. Your booking hasn't changed."),
            _ => throw new InvalidOperationException($"Unhandled update result {result.GetType().Name}."),
        };
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBooking(int id, CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return AntiforgeryFailed();
        }

        var ownerKey = guestSessionService.GetOwnerKey(HttpContext);
        var cancelled = ownerKey is not null && await bookingService.CancelBookingAsync(id, ownerKey, cancellationToken);

        return cancelled ? NoContent() : BookingNotFound();
    }

    private ObjectResult BookingNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Booking not found.");

    private ObjectResult AntiforgeryFailed() =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Missing or invalid antiforgery token.",
            detail: "Reload the page, then try again.");
}
