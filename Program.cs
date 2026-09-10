using System.Globalization;
using System.Threading.RateLimiting;
using Gnist.Games;
using Gnist.Data;
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
builder.Services.AddOptions<PartyOptions>()
    .Bind(builder.Configuration.GetSection("Party"))
    .PostConfigure(options =>
    {
        // Render terminates HTTPS before forwarding to the container. Use its public URL for QR codes.
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            options.PublicBaseUrl = builder.Configuration["RENDER_EXTERNAL_URL"] ?? "";
    });
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var frontend = builder.Configuration["Party:FrontendBaseUrl"];
    if (Uri.TryCreate(frontend, UriKind.Absolute, out var uri))
        policy.WithOrigins(uri.GetLeftPart(UriPartial.Authority)).WithMethods("GET", "POST", "DELETE").AllowAnyHeader();
}));
builder.Services.AddSingleton<FrontendLinks>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<GameCatalog>();
builder.Services.AddSingleton<GameManager>();
builder.Services.AddSingleton<RoomBroadcaster>();
builder.Services.AddSingleton<IPartyStore>(services => DatabaseSetup.Create(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>()));
builder.Services.AddSingleton<RoomPersistence>();
builder.Services.AddHostedService(services => services.GetRequiredService<RoomPersistence>());
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
app.UseCors("Frontend");
// CORS does not protect WebSocket handshakes. Enforce the same origin policy there too.
app.Use(async (context, next) =>
{
    if ((context.Request.Path.StartsWithSegments("/party") || context.Request.Path.StartsWithSegments("/api")) &&
        context.Request.Headers.TryGetValue("Origin", out var origin) &&
        !context.RequestServices.GetRequiredService<FrontendLinks>().AllowsOrigin(origin.ToString(), context.Request))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});
app.UseRateLimiter();
app.MapRazorPages();
app.MapHub<PartyHub>("/party");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/rooms", async (HttpContext context, RoomService rooms, RoomPersistence persistence) =>
{
    // Only the configured frontend can read cross-origin responses. Tokens are never put in URLs.
    if (!context.Request.Headers.ContainsKey("X-Gnist-Request")) return Results.BadRequest();
    try { context.Response.Headers.CacheControl = "no-store"; var receipt = rooms.Create();
        await persistence.SaveAsync(rooms.Get(receipt.Code)); return Results.Ok(receipt); }
    catch (PartyException e) { return Results.BadRequest(new { error = e.Message }); }
}).RequireRateLimiting("create");
app.MapGet("/api/rooms/{code}/qr", (string code, HttpContext context, RoomService rooms, FrontendLinks links) =>
{
    try { rooms.Get(code); } catch (PartyException) { return Results.NotFound(); }
    var joinUrl = links.JoinUrl(code, context.Request.Query["frontend"] == "pages", context.Request);
    if (joinUrl is null) return Results.BadRequest(new { error = "GitHub-sidens adresse er ikke konfigureret på spilserveren." });
    using var data = QRCodeGenerator.GenerateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
    using var svg = new SvgQRCode(data);
    return Results.Text(svg.GetGraphic(8), "image/svg+xml");
});
await app.Services.GetRequiredService<RoomPersistence>().RecoverAsync();
app.Run();
public partial class Program;
