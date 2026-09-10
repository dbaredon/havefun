using Gnist.Models;
using Gnist.Services;
using Microsoft.AspNetCore.SignalR;

namespace Gnist.Hubs;

public sealed class PartyHub(RoomService rooms, GameManager games, RoomBroadcaster broadcaster) : Hub
{
    public async Task<object> Host(string code, string token)
    {
        rooms.ConnectHost(code, token, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, code.ToUpperInvariant());
        await Groups.AddToGroupAsync(Context.ConnectionId, $"host:{code.ToUpperInvariant()}");
        await broadcaster.Publish(rooms.Get(code));
        return new { code };
    }
    public async Task<JoinReceipt> Join(string code, string name, string? token)
    {
        var receipt = rooms.Join(code, name, token, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, receipt.Code);
        await broadcaster.Publish(rooms.Get(receipt.Code));
        return receipt;
    }
    public async Task Start(string? kind, bool quickPlay, RoomSettings settings)
    {
        var (room, _) = rooms.Membership(Context.ConnectionId, true);
        games.Start(room, kind, quickPlay, settings);
        await broadcaster.Publish(room);
    }
    public Task Act(PlayerInput input)
    {
        var (room, id) = rooms.Membership(Context.ConnectionId);
        if (id is null) throw new PartyException("Værten deltager via en separat telefon eller fane.");
        games.Input(room, id, input);
        return Task.CompletedTask;
    }
    public async Task Lobby()
    {
        var (room, _) = rooms.Membership(Context.ConnectionId, true);
        games.Lobby(room);
        await broadcaster.Publish(room);
    }
    public async Task Pause()
    {
        var (room, _) = rooms.Membership(Context.ConnectionId, true);
        games.Pause(room);
        await broadcaster.Publish(room);
    }
    public async Task Leave()
    {
        var room = rooms.Disconnect(Context.ConnectionId, true);
        if (room is null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Code);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"host:{room.Code}");
        await broadcaster.Publish(room);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var room = rooms.Disconnect(Context.ConnectionId);
        if (room is not null) await broadcaster.Publish(room);
        await base.OnDisconnectedAsync(exception);
    }
}

public sealed class FriendlyErrors(ILogger<FriendlyErrors> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try { return await next(invocationContext); }
        catch (PartyException e) { throw new HubException(e.Message); }
        catch (Exception e)
        {
            logger.LogError(e, "Hub method {Method} failed", invocationContext.HubMethodName);
            throw new HubException("Noget gik galt. Prøv igen om et øjeblik.");
        }
    }
}
