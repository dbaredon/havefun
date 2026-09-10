using Microsoft.EntityFrameworkCore;

namespace Gnist.Data;

public interface IPartyStore
{
    string Provider { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomSnapshot>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class MemoryPartyStore : IPartyStore
{
    public string Provider => "Memory";
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<RoomSnapshot>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RoomSnapshot>>([]);
    public Task SaveAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class EfPartyStore(DbContextOptions<PartyDbContext> options) : IPartyStore
{
    // One app instance owns the active games. Serialize commits, but never hold a room lock during DB I/O.
    private readonly SemaphoreSlim writes = new(1, 1);
    public string Provider => options.Extensions.Any(x => x.GetType().Name.Contains("Npgsql")) ? "PostgreSQL" : "SQLite";
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new PartyDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<RoomSnapshot>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new PartyDbContext(options);
        var rooms = await db.Rooms.AsNoTracking().ToArrayAsync(cancellationToken);
        var players = await db.Players.AsNoTracking().ToArrayAsync(cancellationToken);
        var rounds = await db.Rounds.AsNoTracking().ToArrayAsync(cancellationToken);
        var submissions = await db.Submissions.AsNoTracking().ToArrayAsync(cancellationToken);
        var results = await db.Results.AsNoTracking().ToArrayAsync(cancellationToken);
        return rooms.Select(r => new RoomSnapshot(r, players.Where(p => p.RoomId == r.Id).ToArray(),
            rounds.Where(g => g.RoomId == r.Id).Select(g => new RoundSnapshot(g,
                submissions.Where(s => s.RoundId == g.Id).ToArray(), results.Where(s => s.RoundId == g.Id).ToArray())).ToArray())).ToArray();
    }
    public async Task SaveAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await writes.WaitAsync(cancellationToken);
        try
        {
            await using var db = new PartyDbContext(options);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var saved = await db.Rooms.FindAsync([snapshot.Room.Id], cancellationToken);
            if (saved is not null && saved.Revision >= snapshot.Room.Revision) return;
            if (saved is null) db.Rooms.Add(snapshot.Room);
            else db.Entry(saved).CurrentValues.SetValues(snapshot.Room);
            var existingPlayers = await db.Players.Where(p => p.RoomId == snapshot.Room.Id).ToDictionaryAsync(p => p.Id, cancellationToken);
            var existingRounds = await db.Rounds.Where(r => r.RoomId == snapshot.Room.Id).ToDictionaryAsync(r => r.Id, cancellationToken);
            foreach (var player in snapshot.Players)
            {
                var existing = existingPlayers.GetValueOrDefault(player.Id);
                if (existing is null) db.Players.Add(player); else db.Entry(existing).CurrentValues.SetValues(player);
            }
            foreach (var round in snapshot.Rounds)
            {
                var existing = existingRounds.GetValueOrDefault(round.Round.Id);
                if (existing is not null && existing.Status != "Playing" && existing.Status == round.Round.Status) continue;
                var submissions = await db.Submissions.Where(s => s.RoundId == round.Round.Id).ToDictionaryAsync(s => s.PlayerId + ":" + s.Key, cancellationToken);
                var results = await db.Results.Where(s => s.RoundId == round.Round.Id).ToDictionaryAsync(s => s.PlayerId, cancellationToken);
                if (existing is null) db.Rounds.Add(round.Round); else db.Entry(existing).CurrentValues.SetValues(round.Round);
                foreach (var submission in round.Submissions)
                {
                    var current = submissions.GetValueOrDefault(submission.PlayerId + ":" + submission.Key);
                    if (current is null) db.Submissions.Add(submission); else db.Entry(current).CurrentValues.SetValues(submission);
                }
                foreach (var result in round.Results)
                {
                    var current = results.GetValueOrDefault(result.PlayerId);
                    if (current is null) db.Results.Add(result); else db.Entry(current).CurrentValues.SetValues(result);
                }
            }
            // Room totals and round results commit together, preventing double scoring after recovery.
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally { writes.Release(); }
    }
}
