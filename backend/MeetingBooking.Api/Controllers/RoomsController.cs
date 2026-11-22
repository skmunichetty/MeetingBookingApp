using MeetingBooking.Api.Contracts.Rooms;
using MeetingBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeetingBooking.Api.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomsController(IRoomService roomService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RoomResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomResponse>>> GetRooms(CancellationToken cancellationToken)
    {
        var rooms = await roomService.GetRoomsAsync(cancellationToken);
        return Ok(rooms);
    }
}
