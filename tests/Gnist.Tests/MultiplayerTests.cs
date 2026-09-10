using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Gnist.Models;
using Gnist.Services;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gnist.Tests;

public class MultiplayerTests
{
    private static HubConnection Connect(WebApplicationFactory<Program> app) => new HubConnectionBuilder()
        .WithUrl("http://localhost/party", options => {
            options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
        }).Build();

    private static async Task<JsonElement> Until(ChannelReader<JsonElement> reader, Func<JsonElement,bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await foreach (var item in reader.ReadAllAsync(timeout.Token)) if (predicate(item)) return item;
        throw new InvalidOperationException("Ingen tilstand modtaget");
    }
    private static async Task<HostReceipt> Create(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("X-Gnist-Request","test");
        var response = await client.PostAsync("/api/rooms",null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HostReceipt>())!;
    }
    [Fact] public async Task RealSignalRFlow_Create_Qr_Join_Lobby_Play_Results_Reconnect()
    {
        await using var app=new WebApplicationFactory<Program>();
        var client=app.CreateClient();
        var created=await Create(client);
        var qr=await client.GetAsync($"/api/rooms/{created.Code}/qr");
        Assert.Equal(HttpStatusCode.OK,qr.StatusCode);
        Assert.Equal("image/svg+xml",qr.Content.Headers.ContentType!.MediaType);
        Assert.Contains("<svg",await qr.Content.ReadAsStringAsync());
        var landing=await client.GetStringAsync("/");
        Assert.Contains("lang=\"da\"",landing);Assert.Contains("Start en fest",landing);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/join/{created.Code}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/host/{created.Code}")).StatusCode);
        await using var host=Connect(app);await using var a=Connect(app);await using var b=Connect(app);
        var states=Channel.CreateUnbounded<JsonElement>();
        host.On<JsonElement>("HostState",state=>states.Writer.TryWrite(state));
        await host.StartAsync();await a.StartAsync();await b.StartAsync();
        await host.InvokeAsync("Host",created.Code,created.HostToken);
        var pa=await a.InvokeAsync<JoinReceipt>("Join",created.Code,"Tony",(string?)null);
        var pb=await b.InvokeAsync<JoinReceipt>("Join",created.Code,"Emma",(string?)null);
        var lobby=await Until(states.Reader,s=>s.GetProperty("players").GetArrayLength()==2);
        Assert.Equal(JsonValueKind.Null,lobby.GetProperty("game").ValueKind);
        // A normal player cannot start a game or borrow their player token as a host token.
        await Assert.ThrowsAsync<HubException>(()=>a.InvokeAsync("Start","cookie",false,new RoomSettings(5)));
        await host.InvokeAsync("Start","cookie",false,new RoomSettings(5));
        var playing=await Until(states.Reader,s=>s.GetProperty("game").ValueKind==JsonValueKind.Object&&s.GetProperty("game").GetProperty("phase").GetString()=="Playing");
        var id=playing.GetProperty("game").GetProperty("id").GetString()!;
        await a.InvokeAsync("Act",new PlayerInput(id,"tap",null,1));
        await Task.Delay(70);
        await a.InvokeAsync("Act",new PlayerInput(id,"tap",null,2));
        await a.InvokeAsync("Act",new PlayerInput(id,"tap",null,2));
        await b.InvokeAsync("Act",new PlayerInput(id,"tap",null,1));
        var results=await Until(states.Reader,s=>s.GetProperty("game").GetProperty("phase").GetString()=="Results");
        var rows=results.GetProperty("game").GetProperty("results");
        Assert.Equal(pa.PlayerId,rows[0].GetProperty("playerId").GetString());
        Assert.Equal(2,rows[0].GetProperty("value").GetDouble());
        Assert.True(rows[0].GetProperty("winner").GetBoolean());
        Assert.Equal(pb.PlayerId,rows[1].GetProperty("playerId").GetString());
        await a.StopAsync();
        await using var restored=Connect(app);await restored.StartAsync();
        var restoredPlayer=await restored.InvokeAsync<JoinReceipt>("Join",created.Code,"Tony",pa.PlayerToken);
        Assert.Equal(pa.PlayerId,restoredPlayer.PlayerId);
        Assert.Equal(2,app.Services.GetRequiredService<RoomService>().Get(created.Code).Players.Count);
        await host.InvokeAsync("Lobby");
        await Until(states.Reader,s=>s.GetProperty("game").ValueKind==JsonValueKind.Null);
        await restored.InvokeAsync("Leave");
        var left=await Until(states.Reader,s=>s.GetProperty("players").GetArrayLength()==1);
        Assert.Equal("Emma",left.GetProperty("players")[0].GetProperty("name").GetString());
    }
    [Fact] public async Task ApiRejectsUnmarkedCreationAndInvalidRooms()
    {
        await using var app=new WebApplicationFactory<Program>();var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/rooms",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/rooms/NOPE/qr")).StatusCode);
    }
    [Fact] public async Task PrivateDuelChoicesNeverReachOtherPlayers()
    {
        await using var app=new WebApplicationFactory<Program>();var created=await Create(app.CreateClient());
        await using var host=Connect(app);await using var a=Connect(app);await using var b=Connect(app);
        var states=Channel.CreateUnbounded<JsonElement>();b.On<JsonElement>("State",s=>states.Writer.TryWrite(s));
        await host.StartAsync();await a.StartAsync();await b.StartAsync();
        await host.InvokeAsync("Host",created.Code,created.HostToken);
        await a.InvokeAsync<JoinReceipt>("Join",created.Code,"Tony",(string?)null);
        await b.InvokeAsync<JoinReceipt>("Join",created.Code,"Emma",(string?)null);
        await host.InvokeAsync("Start","duel",false,new RoomSettings());
        var playing=await Until(states.Reader,s=>s.GetProperty("game").ValueKind==JsonValueKind.Object&&s.GetProperty("game").GetProperty("phase").GetString()=="Playing");
        var id=playing.GetProperty("game").GetProperty("id").GetString()!;
        await a.InvokeAsync("Act",new PlayerInput(id,"choose:1","rock",1));
        await Task.Delay(350);
        var room=app.Services.GetRequiredService<RoomService>().Get(created.Code);
        var projection=JsonSerializer.Serialize(room.Game!.PublicState(DateTimeOffset.UtcNow));
        Assert.Contains("\"choices\":null",projection);
        await b.InvokeAsync("Act",new PlayerInput(id,"choose:1","scissors",1));
        var result=await Until(states.Reader,s=>s.GetProperty("game").GetProperty("phase").GetString()=="Results");
        Assert.Equal("Tony",result.GetProperty("game").GetProperty("results")[0].GetProperty("name").GetString());
    }
}
