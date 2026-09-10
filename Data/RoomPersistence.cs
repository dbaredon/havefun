using Gnist.Models;
using Gnist.Services;

namespace Gnist.Data;

public sealed class RoomPersistence(IPartyStore store, RoomService rooms, ILogger<RoomPersistence> logger) : BackgroundService
{
    public string Provider => store.Provider;
    public async Task RecoverAsync()
    {
        await store.InitializeAsync();
        foreach (var saved in await store.LoadAsync()) rooms.Restore(Snapshots.Restore(saved));
        logger.LogInformation("Storage ready: {Provider}", store.Provider);
    }
    public async Task SaveAsync(Room room, CancellationToken cancellationToken = default)
    {
        if (store is MemoryPartyStore) return;
        var snapshot = Snapshots.Capture(room);
        try { await store.SaveAsync(snapshot, cancellationToken); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not persist room {Code}", room.Code);
            throw new PartyException("Vi kunne ikke gemme lige nu. Prøv igen om et øjeblik.");
        }
    }
    private async Task CheckpointAsync(CancellationToken cancellationToken)
    {
        foreach (var room in rooms.Rooms)
        {
            try { await SaveAsync(room, cancellationToken); }
            catch (PartyException) { /* A later checkpoint retries; never claim a failed critical write succeeded. */ }
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (store is MemoryPartyStore) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) await CheckpointAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (store is not MemoryPartyStore) await CheckpointAsync(cancellationToken);
    }
}
