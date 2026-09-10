using System.Net;
using Gnist.Models;
using Gnist.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gnist.Tests;

public class FrontendTests
{
    [Theory]
    [InlineData("https://dbaredon.github.io", true, "POST")]
    [InlineData("https://dbaredon.github.io", true, "DELETE")]
    [InlineData("https://dbaredon.github.io.evil.example", false, "POST")]
    [InlineData("https://other.github.io", false, "POST")]
    [InlineData("null", false, "POST")]
    public async Task CorsAndHubRequestsOnlyAllowConfiguredFrontend(string origin, bool allowed, string method)
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Party:FrontendBaseUrl"] = "https://dbaredon.github.io/havefun"
            })));
        using var client = app.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/party/negotiate?negotiateVersion=1");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", method);
        preflight.Headers.Add("Access-Control-Request-Headers", "x-signalr-user-agent");
        using var response = await client.SendAsync(preflight);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed) Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var negotiate = await client.PostAsync("/party/negotiate?negotiateVersion=1", null);
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, negotiate.StatusCode);
    }

    [Fact]
    public void QrLinksUseThePagesSubdirectoryAndLocalLinksStillWork()
    {
        var options = Options.Create(new PartyOptions {
            FrontendBaseUrl = "https://dbaredon.github.io/havefun/",
            PublicBaseUrl = "https://gnist.example.com"
        });
        var links = new FrontendLinks(options);
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http"; request.Host = new HostString("localhost:5180");
        Assert.Equal("https://dbaredon.github.io/havefun/join/?code=X7K2", links.JoinUrl("x7k2", true, request));
        Assert.Equal("https://gnist.example.com/join/X7K2", links.JoinUrl("X7K2", false, request));
        Assert.True(links.AllowsOrigin("https://gnist.example.com", request));
        Assert.False(links.AllowsOrigin("https://gnist.example.com.evil.example", request));
        var local = new FrontendLinks(Options.Create(new PartyOptions()));
        Assert.Equal("http://localhost:5180/join/X7K2", local.JoinUrl("X7K2", false, request));
        Assert.Null(local.JoinUrl("X7K2", true, request));
    }
}
