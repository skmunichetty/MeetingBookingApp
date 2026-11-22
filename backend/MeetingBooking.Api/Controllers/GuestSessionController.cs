using MeetingBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeetingBooking.Api.Controllers;

[ApiController]
[Route("api/guest-session")]
public class GuestSessionController(IGuestSessionService guestSessionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult EnsureGuestSession()
    {
        guestSessionService.EnsureSession(HttpContext);
        return NoContent();
    }
}
