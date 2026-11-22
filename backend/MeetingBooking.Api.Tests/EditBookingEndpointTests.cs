using System.Net;
using System.Net.Http.Json;
using MeetingBooking.Api.Contracts.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace MeetingBooking.Api.Tests;

public sealed class EditBookingEndpointTests : IDisposable
{
    private const int LighthouseId = 3;
    private static readonly DateTimeOffset Now = new(2030, 6, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly MeetingBookingApiFactory _factory = new(new FixedTimeProvider(Now));

    private static object Lighthouse(string start, string end, string title = "Sprint planning", string date = "2030-06-12") =>
        BookingsEndpointTests.Booking(roomId: LighthouseId, meetingTitle: title, date: date, startTime: start, endTime: end);

    private static object Changes(
        string start = "10:00",
        string end = "11:00",
        string title = "Sprint planning",
        string guestName = "Sai",
        string date = "2030-06-12") =>
        new { guestName, meetingTitle = title, date, startTime = start, endTime = end };

    private async Task<(GuestBrowser Browser, BookingResponse Booking)> OwnerWithBookingAsync(string start = "10:00", string end = "11:00")
    {
        var browser = await GuestBrowser.OpenAsync(_factory);
        var booking = await browser.BookOrFailAsync(Lighthouse(start, end));
        return (browser, booking);
    }

    [Fact]
    public async Task GetBooking_ReturnsTheOwnersSavedDetails()
    {
        var (owner, booking) = await OwnerWithBookingAsync();
        using var _ = owner;

        var response = await owner.GetBookingAsync(booking.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(booking, await response.Content.ReadFromJsonAsync<BookingResponse>());
    }

    [Fact]
    public async Task AnotherBrowser_CannotReadUpdateOrCancelTheBooking()
    {
        var (owner, booking) = await OwnerWithBookingAsync();
        using var _ = owner;
        using var other = await GuestBrowser.OpenAsync(_factory);
        await other.GetMineAsync();

        var read = await other.GetBookingAsync(booking.Id);
        var update = await other.UpdateAsync(booking.Id, Changes(title: "Hijacked"));
        var cancel = await other.CancelAsync(booking.Id);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.DoesNotContain("Sprint planning", await read.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal([booking], await owner.GetMineAsync());
    }

    [Fact]
    public async Task TitleOnlyEdit_KeepsTheSameSlotAndId()
    {
        var (owner, booking) = await OwnerWithBookingAsync();
        using var _ = owner;
        await owner.GetBookingAsync(booking.Id);

        var response = await owner.UpdateAsync(booking.Id, Changes(title: "  Team planning  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(booking with { MeetingTitle = "Team planning" }, updated);
        Assert.Equal([updated!], await owner.GetMineAsync());
    }

    [Fact]
    public async Task Edit_CanMoveIntoTimeOverlappingItsOwnOldSlot()
    {
        var (owner, booking) = await OwnerWithBookingAsync("10:00", "11:00");
        using var _ = owner;
        await owner.GetBookingAsync(booking.Id);

        var response = await owner.UpdateAsync(booking.Id, Changes("10:30", "11:30", guestName: "Sai K"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(booking with { GuestName = "Sai K", StartTime = "10:30", EndTime = "11:30" }, updated);
    }

    [Fact]
    public async Task Edit_ConflictingWithAnotherBooking_Returns409AndKeepsTheOriginal()
    {
        var (owner, booking) = await OwnerWithBookingAsync("10:00", "11:00");
        using var _ = owner;
        using var other = await GuestBrowser.OpenAsync(_factory);
        await other.BookOrFailAsync(Lighthouse("13:00", "14:00", title: "Someone else"));
        await owner.GetBookingAsync(booking.Id);

        var response = await owner.UpdateAsync(booking.Id, Changes("13:30", "14:30", title: "Moved planning"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("That time is already booked.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Title);
        Assert.Equal(booking, await (await owner.GetBookingAsync(booking.Id)).Content.ReadFromJsonAsync<BookingResponse>());
        Assert.Equal(HttpStatusCode.Conflict, (await other.BookAsync(Lighthouse("10:00", "11:00"))).StatusCode);
    }

    [Fact]
    public async Task Edit_ToASlotFreedByCancellation_Succeeds()
    {
        var (owner, booking) = await OwnerWithBookingAsync("10:00", "11:00");
        using var _ = owner;
        var blocker = await owner.BookOrFailAsync(Lighthouse("12:00", "13:00", title: "Blocker"));
        await owner.GetMineAsync();
        await owner.CancelAsync(blocker.Id);

        var response = await owner.UpdateAsync(booking.Id, Changes("12:00", "13:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_WithoutAntiforgeryToken_IsRejectedAndChangesNothing()
    {
        var (owner, booking) = await OwnerWithBookingAsync();
        using var _ = owner;

        var response = await owner.UpdateAsync(booking.Id, Changes(title: "No token"), sendAntiforgeryToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([booking], await owner.GetMineAsync());
    }

    public static TheoryData<string, object> InvalidChanges => new()
    {
        { "guestName", Changes(guestName: "   ") },
        { "meetingTitle", Changes(title: "") },
        { "endTime", Changes("11:00", "11:00") },
        { "endTime", Changes("11:00", "10:00") },
        { "date", Changes(date: "12/06/2030") },
    };

    [Theory]
    [MemberData(nameof(InvalidChanges))]
    public async Task Edit_InvalidDetails_Returns400AndChangesNothing(string field, object changes)
    {
        var (owner, booking) = await OwnerWithBookingAsync();
        using var _ = owner;
        await owner.GetBookingAsync(booking.Id);

        var response = await owner.UpdateAsync(booking.Id, changes);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, key => string.Equals(key, field, StringComparison.OrdinalIgnoreCase));
        Assert.Equal([booking], await owner.GetMineAsync());
    }

    [Fact]
    public async Task ReadOrEdit_CancelledOrUnknownBooking_Returns404()
    {
        var (owner, booking) = await OwnerWithBookingAsync();
        using var _ = owner;
        await owner.GetMineAsync();
        await owner.CancelAsync(booking.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetBookingAsync(booking.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.UpdateAsync(booking.Id, Changes())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetBookingAsync(424242)).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
