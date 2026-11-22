using System.Net;
using System.Net.Http.Json;
using MeetingBooking.Api.Contracts.Bookings;
using MeetingBooking.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingBooking.Api.Tests;

public sealed class BookingsEndpointTests : IDisposable
{
    private const int OrionId = 1;
    private const int CedarId = 2;

    private readonly MeetingBookingApiFactory _factory = new();
    private GuestBrowser? _browser;

    internal static object Booking(
        int roomId = OrionId,
        string? guestName = "Sai",
        string? meetingTitle = "Sprint planning",
        string? date = "2030-06-12",
        string? startTime = "10:00",
        string? endTime = "11:00") =>
        new { roomId, guestName, meetingTitle, date, startTime, endTime };

    private async Task<HttpResponseMessage> PostAsync(object body)
    {
        _browser ??= await GuestBrowser.OpenAsync(_factory);
        return await _browser.BookAsync(body);
    }

    [Fact]
    public async Task CreateBooking_SavesTrimmedDetailsAndReturns201()
    {
        var response = await PostAsync(Booking(guestName: "  Sai  ", meetingTitle: "  Sprint planning "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);
        Assert.True(booking.Id > 0);
        Assert.Equal(
            new BookingResponse(booking.Id, OrionId, "Orion", "Sai", "Sprint planning", "2030-06-12", "10:00", "11:00"),
            booking);

        using var scope = _factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Bookings.SingleAsync();
        Assert.Equal(("Sai", new TimeOnly(10, 0), new TimeOnly(11, 0)), (saved.GuestName, saved.StartTime, saved.EndTime));
        Assert.Matches("^[0-9a-f]{64}$", saved.OwnerKeyHash);
        Assert.NotEqual(_browser!.GuestToken, saved.OwnerKeyHash);
        Assert.DoesNotContain(_browser.GuestToken, body);
    }

    [Fact]
    public async Task CreateBooking_WithoutGuestSession_Returns400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/bookings", Booking());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Guest session required.", problem?.Title);
    }

    [Fact]
    public async Task GuestSessionCookie_IsHttpOnlySameSiteAndScopedToTheApi()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/guest-session");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var guestCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("MeetingBooking.Guest="));
        Assert.Contains("httponly", guestCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", guestCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api", guestCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=", guestCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBooking_OverlappingTimeInSameRoom_Returns409()
    {
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(Booking(startTime: "10:00", endTime: "11:00"))).StatusCode);

        var conflict = await PostAsync(Booking(guestName: "Alex", startTime: "10:30", endTime: "11:30"));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("That time is already booked.", problem?.Title);
    }

    [Theory]
    [InlineData("09:00", "10:30")]
    [InlineData("10:15", "10:45")]
    [InlineData("09:00", "12:00")]
    [InlineData("10:00", "11:00")]
    public async Task CreateBooking_AnyOverlap_Returns409(string start, string end)
    {
        await PostAsync(Booking(startTime: "10:00", endTime: "11:00"));

        var response = await PostAsync(Booking(startTime: start, endTime: end));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_BackToBackMeetings_AreBothAllowed()
    {
        var first = await PostAsync(Booking(startTime: "10:00", endTime: "11:00"));
        var second = await PostAsync(Booking(startTime: "11:00", endTime: "12:00"));
        var before = await PostAsync(Booking(startTime: "09:00", endTime: "10:00"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, before.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_SameTimeInAnotherRoomOrDay_IsAllowed()
    {
        await PostAsync(Booking(roomId: OrionId, date: "2030-06-12"));

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(Booking(roomId: CedarId, date: "2030-06-12"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(Booking(roomId: OrionId, date: "2030-06-13"))).StatusCode);
    }

    [Fact]
    public async Task CreateBooking_UnknownRoom_Returns404()
    {
        var response = await PostAsync(Booking(roomId: 999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string, object> InvalidRequests => new()
    {
        { "guestName", Booking(guestName: "   ") },
        { "guestName", Booking(guestName: null) },
        { "guestName", Booking(guestName: new string('a', 101)) },
        { "meetingTitle", Booking(meetingTitle: " \t ") },
        { "date", Booking(date: "01/12/2026") },
        { "startTime", Booking(startTime: "10am") },
        { "endTime", Booking(endTime: "") },
        { "endTime", Booking(startTime: "11:00", endTime: "11:00") },
        { "endTime", Booking(startTime: "11:00", endTime: "10:00") },
        { "roomId", Booking(roomId: 0) },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CreateBooking_InvalidInput_Returns400WithFieldError(string field, object body)
    {
        var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, key => string.Equals(key, field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateBooking_SimultaneousRequestsForSameSlot_SaveExactlyOne()
    {
        const int attempts = 12;

        var browsers = await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ => GuestBrowser.OpenAsync(_factory)));
        using var start = new ManualResetEventSlim(false);
        var requests = browsers.Select((browser, i) => Task.Run(async () =>
        {
            start.Wait();
            return await browser.BookAsync(Booking(guestName: $"Guest {i}", startTime: "10:00", endTime: "11:00"));
        })).ToList();
        start.Set();

        var responses = await Task.WhenAll(requests);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(attempts - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Bookings.CountAsync());
        foreach (var browser in browsers)
        {
            browser.Dispose();
        }
    }

    public void Dispose()
    {
        _browser?.Dispose();
        _factory.Dispose();
    }
}
