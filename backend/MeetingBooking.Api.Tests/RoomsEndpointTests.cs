using System.Net;
using System.Net.Http.Json;
using MeetingBooking.Api.Contracts.Rooms;

namespace MeetingBooking.Api.Tests;

public sealed class RoomsEndpointTests : IDisposable
{
    private readonly MeetingBookingApiFactory _factory = new();

    [Fact]
    public async Task GetRooms_ReturnsSeededRoomsOrderedByName()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var json = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var first = json.RootElement.EnumerateArray().First();
        Assert.Equal(["id", "name", "capacity"], first.EnumerateObject().Select(p => p.Name));

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.NotNull(rooms);
        Assert.Equal(["Cedar", "Lighthouse", "Orion"], rooms.Select(r => r.Name));
        Assert.Equal([4, 8, 12], rooms.Select(r => r.Capacity));
        Assert.True(File.Exists(_factory.DatabasePath), "Expected the isolated test database to be created.");
    }

    [Fact]
    public async Task GetRooms_IsPublic()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
