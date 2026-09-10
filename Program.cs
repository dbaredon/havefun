using System.Globalization;
using System.Threading.RateLimiting;
using Gnist.Games;
using Gnist.Hubs;
using Gnist.Models;
using Gnist.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using QRCoder;

var builder = WebApplication.CreateBuilder(args);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("da-DK");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("da-DK");
builder.Services.Configure<PartyOptions>(builder.Configuration.GetSection("Party"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<GameCatalog>();
builder.Services.AddSingleton<GameManager>();
builder.Services.AddSingleton<RoomBroadcaster>();
builder.Services.AddHostedService<RoomTicker>();
builder.Services.AddRazorPages();
builder.Services.AddSignalR(o => { o.MaximumReceiveMessageSize = 4096; o.AddFilter<FriendlyErrors>(); });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("create", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.MapRazorPages();
app.MapHub<PartyHub>("/party");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/rooms", (HttpContext context, RoomService rooms) =>
{
    // No cross-origin creation; host tokens are returned only to the requesting browser.
    if (!context.Request.Headers.ContainsKey("X-Gnist-Request")) return Results.BadRequest();
    try { context.Response.Headers.CacheControl = "no-store"; return Results.Ok(rooms.Create()); }
    catch (PartyException e) { return Results.BadRequest(new { error = e.Message }); }
}).RequireRateLimiting("create");
app.MapGet("/api/rooms/{code}/qr", (string code, HttpContext context, RoomService rooms, IOptions<PartyOptions> options) =>
{
    try { rooms.Get(code); } catch (PartyException) { return Results.NotFound(); }
    var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
    if (baseUrl.Length == 0) baseUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    using var data = QRCodeGenerator.GenerateQrCode($"{baseUrl}/join/{Uri.EscapeDataString(code.ToUpperInvariant())}", QRCodeGenerator.ECCLevel.Q);
    using var svg = new SvgQRCode(data);
    return Results.Text(svg.GetGraphic(8), "image/svg+xml");
});
app.Run();
public partial class Program;
