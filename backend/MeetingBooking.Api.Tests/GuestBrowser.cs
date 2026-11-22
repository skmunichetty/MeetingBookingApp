using System.Net.Http.Json;
using MeetingBooking.Api.Contracts.Bookings;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MeetingBooking.Api.Tests;

public sealed class GuestBrowser : IDisposable
{
    private GuestBrowser(HttpClient client) => Client = client;

    public HttpClient Client { get; }

    public string GuestToken { get; private set; } = string.Empty;

    public string XsrfToken { get; private set; } = string.Empty;

    public static async Task<GuestBrowser> OpenAsync(WebApplicationFactory<Program> factory)
    {
        var browser = new GuestBrowser(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        }));

        var session = await browser.Client.GetAsync("/api/guest-session");
        session.EnsureSuccessStatusCode();
        browser.GuestToken = ReadCookie(session, "MeetingBooking.Guest")!;
        browser.XsrfToken = ReadCookie(session, "XSRF-TOKEN")!;
        return browser;
    }

    public Task<HttpResponseMessage> BookAsync(object body) => Client.PostAsJsonAsync("/api/bookings", body);

    public async Task<BookingResponse> BookOrFailAsync(object body)
    {
        var response = await BookAsync(body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    public async Task<List<BookingResponse>> GetMineAsync()
    {
        var response = await Client.GetAsync("/api/bookings/mine");
        response.EnsureSuccessStatusCode();
        XsrfToken = ReadCookie(response, "XSRF-TOKEN") ?? XsrfToken;
        return (await response.Content.ReadFromJsonAsync<List<BookingResponse>>())!;
    }

    public async Task<HttpResponseMessage> GetBookingAsync(int bookingId)
    {
        var response = await Client.GetAsync($"/api/bookings/{bookingId}");
        XsrfToken = ReadCookie(response, "XSRF-TOKEN") ?? XsrfToken;
        return response;
    }

    public Task<HttpResponseMessage> UpdateAsync(int bookingId, object body, bool sendAntiforgeryToken = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/bookings/{bookingId}")
        {
            Content = JsonContent.Create(body),
        };
        if (sendAntiforgeryToken)
        {
            request.Headers.Add("X-XSRF-TOKEN", XsrfToken);
        }

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> CancelAsync(int bookingId, bool sendAntiforgeryToken = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/bookings/{bookingId}");
        if (sendAntiforgeryToken)
        {
            request.Headers.Add("X-XSRF-TOKEN", XsrfToken);
        }

        return Client.SendAsync(request);
    }

    private static string? ReadCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var cookie = cookies.FirstOrDefault(value => value.StartsWith(name + "=", StringComparison.Ordinal));
        return cookie is null ? null : Uri.UnescapeDataString(cookie.Split(';')[0][(name.Length + 1)..]);
    }

    public void Dispose() => Client.Dispose();
}
