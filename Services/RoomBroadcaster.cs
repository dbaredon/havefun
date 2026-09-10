using Gnist.Hubs;
using Gnist.Models;
using Microsoft.AspNetCore.SignalR;

namespace Gnist.Services;

public sealed class RoomBroadcaster(IHubContext<PartyHub> hub, GameManager games)
{
    public Task Publish(Room room)
    {
        // Queue immutable snapshots under the room lock to keep message order consistent.
        lock (room.Gate)
        {
            if (room.Closed) return Task.CompletedTask;
            List<Task> sends = [
                hub.Clients.Group(room.Code).SendAsync("State", games.Snapshot(room)),
                hub.Clients.Group($"host:{room.Code}").SendAsync("HostState", games.Snapshot(room, true))
            ];
            foreach (var player in room.Players.Values.Where(p => p.Connected))
                sends.Add(hub.Clients.Clients(player.Connections.ToArray()).SendAsync("Own", new {
                    playerId = player.Id, roundId = room.Game?.Id, state = room.Game?.PrivateState(player.Id)
                }));
            return Task.WhenAll(sends);
        }
    }
    public Task Closed(Room room) => hub.Clients.Group(room.Code).SendAsync("Closed", "Rummet er udløbet. Start en ny fest.");
}

public sealed class RoomTicker(RoomService rooms, GameManager games, RoomBroadcaster broadcaster, TimeProvider clock, ILogger<RoomTicker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50), clock);
        var tick = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            tick++;
            foreach (var room in rooms.Rooms)
            {
                try
                {
                    if (tick % 100 == 0 && rooms.Expire(room, clock.GetUtcNow())) { await broadcaster.Closed(room); continue; }
                    games.Tick(room);
                    if (tick % 5 == 0 || room.Game is { Kind: "reaction", FinishedAt: null }) await broadcaster.Publish(room);
                }
                catch (Exception e) { logger.LogError(e, "Room tick failed for {Code}", room.Code); }
            }
        }
    }
}
