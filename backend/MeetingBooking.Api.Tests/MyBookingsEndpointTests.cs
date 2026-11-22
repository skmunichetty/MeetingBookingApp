using System.Net;
using System.Net.Http.Json;
using MeetingBooking.Api.Contracts.Bookings;
using MeetingBooking.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MeetingBooking.Api.Tests;

public sealed class MyBookingsEndpointTests : IDisposable
{
    private const int LighthouseId = 3;
    private static readonly DateTimeOffset Now = new(2030, 6, 10, 10, 30, 0, TimeSpan.Zero);

    private readonly MeetingBookingApiFactory _factory = new(new FixedTimeProvider(Now));

    private static object Lighthouse(string date, string start, string end, string guestName = "Sai", string title = "Planning") =>
        BookingsEndpointTests.Booking(roomId: LighthouseId, guestName: guestName, meetingTitle: title, date: date, startTime: start, endTime: end);

    [Fact]
    public async Task TwoBrowsersUsingTheSameName_EachSeeOnlyTheirOwnBookings()
    {
        using var browserA = await GuestBrowser.OpenAsync(_factory);
        using var browserB = await GuestBrowser.OpenAsync(_factory);

        var bookingA = await browserA.BookOrFailAsync(Lighthouse("2030-06-12", "10:00", "11:00"));
        var bookingB = await browserB.BookOrFailAsync(Lighthouse("2030-06-12", "13:00", "14:00"));

        Assert.Equal([bookingA], await browserA.GetMineAsync());
        Assert.Equal([bookingB], await browserB.GetMineAsync());
    }

    [Fact]
    public async Task AnotherBrowser_CannotCancelByIdAndGets404()
    {
        using var owner = await GuestBrowser.OpenAsync(_factory);
        using var other = await GuestBrowser.OpenAsync(_factory);
        var booking = await owner.BookOrFailAsync(Lighthouse("2030-06-12", "10:00", "11:00"));
        await other.GetMineAsync();

        var response = await other.CancelAsync(booking.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([booking], await owner.GetMineAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await other.BookAsync(Lighthouse("2030-06-12", "10:00", "11:00"))).StatusCode);
    }

    [Fact]
    public async Task Cancel_IsSafeToRetry_RemovesFromTheListAndFreesTheSlot()
    {
        using var owner = await GuestBrowser.OpenAsync(_factory);
        using var other = await GuestBrowser.OpenAsync(_factory);
        var booking = await owner.BookOrFailAsync(Lighthouse("2030-06-12", "10:00", "11:00"));
        await owner.GetMineAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.CancelAsync(booking.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.CancelAsync(booking.Id)).StatusCode);

        Assert.Empty(await owner.GetMineAsync());
        Assert.Equal(HttpStatusCode.Created, (await other.BookAsync(Lighthouse("2030-06-12", "10:00", "11:00"))).StatusCode);
    }

    [Fact]
    public async Task Cancel_WithoutAntiforgeryToken_IsRejectedAndKeepsTheBooking()
    {
        using var owner = await GuestBrowser.OpenAsync(_factory);
        var booking = await owner.BookOrFailAsync(Lighthouse("2030-06-12", "10:00", "11:00"));

        var response = await owner.CancelAsync(booking.Id, sendAntiforgeryToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([booking], await owner.GetMineAsync());
    }

    [Fact]
    public async Task Cancel_UnknownBooking_Returns404()
    {
        using var browser = await GuestBrowser.OpenAsync(_factory);

        Assert.Equal(HttpStatusCode.NotFound, (await browser.CancelAsync(98765)).StatusCode);
    }

    [Fact]
    public async Task Mine_WithNoBookings_ReturnsAnEmptyArray()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mine_ListsOngoingAndUpcomingInOrder_AndHidesEndedAndCancelled()
    {
        using var browser = await GuestBrowser.OpenAsync(_factory);
        var laterToday = await browser.BookOrFailAsync(Lighthouse("2030-06-10", "12:00", "13:00", title: "Later today"));
        var tomorrow = await browser.BookOrFailAsync(Lighthouse("2030-06-11", "09:00", "10:00", title: "Tomorrow"));
        var ongoing = await browser.BookOrFailAsync(Lighthouse("2030-06-10", "10:00", "11:00", title: "Ongoing"));
        await browser.BookOrFailAsync(Lighthouse("2030-06-10", "08:00", "09:00", title: "Ended this morning"));
        await browser.BookOrFailAsync(Lighthouse("2030-06-09", "10:00", "11:00", title: "Yesterday"));
        var cancelled = await browser.BookOrFailAsync(Lighthouse("2030-06-13", "10:00", "11:00", title: "Cancelled"));
        await browser.GetMineAsync();
        await browser.CancelAsync(cancelled.Id);

        var mine = await browser.GetMineAsync();

        Assert.Equal([ongoing, laterToday, tomorrow], mine);
        Assert.DoesNotContain(browser.GuestToken, System.Text.Json.JsonSerializer.Serialize(mine));
    }

    [Fact]
    public async Task BookingFromBeforeOwnership_IsPreserved_StillBlocks_AndIsNeverClaimedByName()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"meeting-booking-legacy-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        await using (var oldDatabase = new BookingDbContext(options))
        {
            await oldDatabase.GetService<IMigrator>().MigrateAsync("20261122211537_AddBookings");
            await oldDatabase.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Bookings" ("RoomId", "GuestName", "MeetingTitle", "Date", "StartTime", "EndTime", "CreatedAtUtc")
                VALUES (3, 'Sai', 'Booked before ownership', '2030-06-12', '10:00:00', '11:00:00', '2026-11-22 21:00:00');
                """);
        }

        using var factory = new MeetingBookingApiFactory(new FixedTimeProvider(Now), databasePath);
        using var sai = await GuestBrowser.OpenAsync(factory);

        Assert.Equal(HttpStatusCode.Conflict, (await sai.BookAsync(Lighthouse("2030-06-12", "10:30", "11:30"))).StatusCode);
        Assert.Empty(await sai.GetMineAsync());

        await using var migrated = new BookingDbContext(options);
        var legacy = await migrated.Bookings.SingleAsync();
        Assert.Equal(("Booked before ownership", null, null), (legacy.MeetingTitle, legacy.OwnerKeyHash, legacy.CancelledAtUtc));
        Assert.Equal(HttpStatusCode.NotFound, (await sai.CancelAsync(legacy.Id)).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
