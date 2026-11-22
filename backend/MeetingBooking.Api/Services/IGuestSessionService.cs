namespace MeetingBooking.Api.Services;

public interface IGuestSessionService
{
    string EnsureSession(HttpContext httpContext);

    string? GetOwnerKey(HttpContext httpContext);
}
