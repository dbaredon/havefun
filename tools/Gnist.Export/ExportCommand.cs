using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gnist.Export;

internal static partial class ExportCommand
{
    public static async Task Main(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/pages");
        if (!File.Exists(Path.Combine(root, "Gnist.csproj")))
            throw new InvalidOperationException("Kør eksporten fra projektets rod.");
        var basePath = "/" + (Environment.GetEnvironmentVariable("GNIST_BASE_PATH") ?? "havefun").Trim('/') + "/";
        if (basePath == "//") basePath = "/";
        if (!SafePath().IsMatch(basePath)) throw new InvalidOperationException("Ugyldig sti til GitHub Pages.");
        var apiUrl = (Environment.GetEnvironmentVariable("GNIST_API_URL") ?? "").Trim().TrimEnd('/');
        if (apiUrl.Length > 0 && (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0))
            throw new InvalidOperationException("GNIST_API_URL skal være spilserverens HTTPS-origin, fx https://gnist.onrender.com.");

        Directory.CreateDirectory(output);
        await using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder => builder.UseContentRoot(root).UseEnvironment("Development"));
        using var client = factory.CreateClient();
        foreach (var (route, file) in new[] { ("/", "index.html"), ("/join", "join/index.html"), ("/host/ROOM", "host/index.html") })
        {
            var html = await client.GetStringAsync(route);
            html = html.Replace("<body ", "<body data-static-site=\"true\" ");
            // Render the real Razor views at build time; do not maintain a second UI.
            html = UrlAttribute().Replace(html, match =>
            {
                var path = WebUtility.HtmlDecode(match.Groups[2].Value).Split('?')[0];
                if (path.StartsWith("/api/")) return ""; // Room-specific QR is loaded by the client after identification.
                if (path == "/join") path = "/join/";
                return $"{match.Groups[1].Value}=\"{basePath}{path.TrimStart('/')}\"";
            });
            html = html.Replace("/ROOM", "/").Replace(">ROOM<", ">—<");
            html = html.Replace("    <script data-gnist-app", $"    <script src=\"{basePath}config.js\" defer></script>\n    <script data-gnist-app");
            var target = Path.Combine(output, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, html);
        }
        foreach (var source in Directory.EnumerateFiles(Path.Combine(root, "wwwroot"), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(output, Path.GetRelativePath(Path.Combine(root, "wwwroot"), source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
        }
        await File.WriteAllTextAsync(Path.Combine(output, "config.js"),
            "window.GNIST_CONFIG = " + JsonSerializer.Serialize(new { apiBaseUrl = apiUrl }) + ";\n");
        await File.WriteAllTextAsync(Path.Combine(output, ".nojekyll"), "");
        await File.WriteAllTextAsync(Path.Combine(output, "404.html"), $"""
            <!doctype html><html lang="da"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Siden findes ikke · GNIST</title><link rel="stylesheet" href="{basePath}css/site.css">
            <main class="player-shell"><h1>Festen starter her.</h1><p>Dette link findes ikke. Åbn forsiden, eller indtast din rumkode.</p>
            <a class="button primary" href="{basePath}">Til forsiden →</a><a class="button secondary" href="{basePath}join/">Deltag med kode</a></main></html>
            """);
        Console.WriteLine($"GitHub Pages: {output} (sti: {basePath})");
        Console.WriteLine(apiUrl.Length == 0 ? "Brugerfladen er klar; sæt GNIST_API_URL for at aktivere multiplayer." : $"Spilserver: {apiUrl}");
    }

    [GeneratedRegex("(href|src)=\"(/[^\"]*)\"")]
    private static partial Regex UrlAttribute();
    [GeneratedRegex(@"^/(?:[a-zA-Z0-9_.-]+/)*$")]
    private static partial Regex SafePath();
}
