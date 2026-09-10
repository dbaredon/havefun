using Gnist.Games;
using Gnist.Models;
using Gnist.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gnist.Tests;

public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
public class RoomTests
{
    private readonly ManualClock clock=new();
    private RoomService Rooms(int maxPlayers=100)=>new(Options.Create(new PartyOptions{MaxPlayers=maxPlayers,RoomIdleMinutes=5}),clock);
    [Fact] public void CreatesUniqueCodesAndSeparateSecretHostTokens()
    {
        var service=Rooms();
        var rooms=Enumerable.Range(0,200).Select(_=>service.Create()).ToArray();
        Assert.Equal(200,rooms.Select(r=>r.Code).Distinct().Count());
        Assert.All(rooms,r=>{Assert.Equal(4,r.Code.Length);Assert.Equal(64,r.HostToken.Length);});
    }
    [Fact] public void JoinDifferentiatesNamesAndReconnectKeepsIdentity()
    {
        var service=Rooms();var room=service.Create();
        var a=service.Join(room.Code,"Tony",null,"a");
        var b=service.Join(room.Code,"tony",null,"b");
        Assert.Equal("tony 2",b.Name);
        service.Disconnect("a");
        var restored=service.Join(room.Code,"Other",a.PlayerToken,"new-a");
        Assert.Equal(a.PlayerId,restored.PlayerId);Assert.Equal("Tony",restored.Name);
        Assert.Equal(2,service.Get(room.Code).Players.Count);
    }
    [Fact] public void PlayerCannotBecomeHostOrControlHostMethods()
    {
        var service=Rooms();var room=service.Create();
        var a=service.Join(room.Code,"Tony",null,"a");
        Assert.Throws<PartyException>(()=>service.ConnectHost(room.Code,a.PlayerToken,"evil"));
        Assert.Throws<PartyException>(()=>service.Membership("a",true));
        service.ConnectHost(room.Code,room.HostToken,"host");
        Assert.Null(service.Membership("host",true).PlayerId);
    }
    [Fact] public void OneConnectionCannotJoinMultiplePlayersOrRooms()
    {
        var service=Rooms();var room=service.Create();
        service.Join(room.Code,"Tony",null,"a");
        Assert.Throws<PartyException>(()=>service.Join(room.Code,"Emma",null,"a"));
        Assert.Single(service.Get(room.Code).Players);
    }
    [Fact] public void InvalidRoomNamesAndCapacityAreHandled()
    {
        var service=Rooms(1);var room=service.Create();
        Assert.Throws<PartyException>(()=>service.Join("NOPE","Tony",null,"a"));
        Assert.Throws<PartyException>(()=>service.Join(room.Code,"  ",null,"a"));
        service.Join(room.Code,"Tony",null,"a");
        Assert.Throws<PartyException>(()=>service.Join(room.Code,"Emma",null,"b"));
    }
    [Fact] public void InactiveRoomExpiresEvenWithIdleConnections()
    {
        var service=Rooms();var receipt=service.Create();var room=service.Get(receipt.Code);
        Assert.False(service.Expire(room,clock.Now.AddMinutes(4)));
        Assert.True(service.Expire(room,clock.Now.AddMinutes(6)));
        Assert.Throws<PartyException>(()=>service.Get(receipt.Code));
    }
    [Fact] public void LateJoinWaitsUntilNextRoundAndPointsAwardOnlyOnce()
    {
        var service=Rooms();var receipt=service.Create();var room=service.Get(receipt.Code);
        var a=service.Join(room.Code,"Tony",null,"a");
        var manager=new GameManager(new(),clock);
        manager.Start(room,"cookie",false,new());
        var b=service.Join(room.Code,"Emma",null,"b");
        Assert.DoesNotContain(b.PlayerId,room.Game!.Players);
        clock.Now=room.Game.StartsAt.AddSeconds(1);
        manager.Input(room,a.PlayerId,new(room.Game.Id,"tap",null,1));
        clock.Now=clock.Now.AddSeconds(12);
        manager.Tick(room);manager.Tick(room);
        Assert.Equal(3,room.Players[a.PlayerId].Score);
        clock.Now=clock.Now.AddSeconds(2);
        manager.Start(room,"timing",false,new());
        Assert.Contains(b.PlayerId,room.Game.Players);
    }
    [Fact] public void QuickPlayWaitsForHostAndPausePreventsNextRound()
    {
        var service=Rooms();var receipt=service.Create();var room=service.Get(receipt.Code);
        service.Join(room.Code,"Tony",null,"a");
        var manager=new GameManager(new(),clock);
        manager.Start(room,null,true,new());
        clock.Now=room.Game!.StartsAt.AddMinutes(1);manager.Tick(room);
        clock.Now=clock.Now.AddSeconds(2);manager.Tick(room);
        Assert.Null(room.NextRoundAt);
        service.ConnectHost(room.Code,receipt.HostToken,"host");manager.Tick(room);
        Assert.NotNull(room.NextRoundAt);
        manager.Pause(room);clock.Now=clock.Now.AddMinutes(1);manager.Tick(room);
        Assert.Equal(1,room.Round);
    }
}
