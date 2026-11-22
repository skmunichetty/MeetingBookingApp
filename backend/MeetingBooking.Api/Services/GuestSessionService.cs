using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.WebUtilities;

namespace MeetingBooking.Api.Services;

public class GuestSessionService(IAntiforgery antiforgery, IWebHostEnvironment environment) : IGuestSessionService
{
    public const string GuestCookieName = "MeetingBooking.Guest";

    public const string XsrfCookieName = "XSRF-TOKEN";

    private const int TokenByteLength = 32;
    private static readonly TimeSpan GuestCookieLifetime = TimeSpan.FromDays(365);

    public string EnsureSession(HttpContext httpContext)
    {
        var token = ReadValidToken(httpContext)
            ?? WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenByteLength));

        httpContext.Response.Cookies.Append(GuestCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = UseSecureCookies(httpContext),
            Path = "/api",
            MaxAge = GuestCookieLifetime,
            IsEssential = true,
        });

        var antiforgeryTokens = antiforgery.GetAndStoreTokens(httpContext);
        httpContext.Response.Cookies.Append(XsrfCookieName, antiforgeryTokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = UseSecureCookies(httpContext),
            Path = "/",
        });

        return Hash(token);
    }

    public string? GetOwnerKey(HttpContext httpContext) =>
        ReadValidToken(httpContext) is { } token ? Hash(token) : null;

    private static string? ReadValidToken(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(GuestCookieName, out var token) || string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return WebEncoders.Base64UrlDecode(token).Length == TokenByteLength ? token : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    private bool UseSecureCookies(HttpContext httpContext) =>
        httpContext.Request.IsHttps || !environment.IsDevelopment();
}
