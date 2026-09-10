using Gnist.Games;
using Gnist.Data;
using Gnist.Models;

namespace Gnist.Services;

public sealed class GameManager(GameCatalog catalog, TimeProvider clock)
{
    public void Start(Room room, string? kind, bool quick, RoomSettings settings)
    {
        lock (room.Gate)
        {
            RoomService.EnsureOpen(room);
            var now = clock.GetUtcNow();
            if (room.Game is not null && room.Game.Phase(now) != "Results") throw new PartyException("Vent, til den nuværende runde er slut.");
            if (settings.ClickSeconds is < 10 or > 60)
                throw new PartyException("Klikamok skal vare mellem 10 og 60 sekunder.");
            var players = room.Players.Values.Where(p => p.Connected).Select(p => p.Id).ToArray();
            if (players.Length == 0) throw new PartyException("Vent på mindst én spiller.");
            kind = quick ? catalog.RandomNext(room.PreviousGame, players.Length) : kind;
            var game = catalog.Create(kind ?? "", players, now, settings);
            room.Recovered = false;
            room.Settings = settings;
            room.QuickPlay = quick;
            room.Game = game;
            room.PreviousGame = kind;
            room.Round++;
            room.NextRoundAt = null;
            room.ResultsStartedAt = null;
            room.ResultsLeaderboardOpen = false;
            room.ResultsElapsedMs = 0;
            room.LastActivity = now;
        }
    }
    public void Input(Room room, string playerId, PlayerInput input)
    {
        lock (room.Gate)
        {
            RoomService.EnsureOpen(room);
            if (input.Value?.Length > 100 || input.Action.Length > 30) return;
            var now = clock.GetUtcNow();
            if (room.Game is HotPotato && input.Action == "pass" &&
                (!room.Players.TryGetValue(input.Value ?? "", out var target) || !target.Connected)) return;
            room.Game?.HandleInput(playerId, input, now);
            if (room.Game?.FinishedAt is not null) Tick(room);
            room.LastActivity = now;
        }
    }
    public void Lobby(Room room)
    {
        lock (room.Gate)
        {
            room.QuickPlay = false;
            room.NextRoundAt = null;
            if (room.Game is { } game && !room.ArchivedRounds.ContainsKey(game.Id))
                room.ArchivedRounds[game.Id] = Snapshots.CaptureRound(room, "Cancelled");
            room.Game = null;
            room.LastActivity = clock.GetUtcNow();
        }
    }
    public void Pause(Room room)
    {
        lock (room.Gate) { room.QuickPlay = false; room.NextRoundAt = null; room.LastActivity = clock.GetUtcNow(); }
    }
    public void Tick(Room room)
    {
        lock (room.Gate)
        {
            var now = clock.GetUtcNow();
            if (room.Closed || room.Game is not { } game) return;
            // Tick before recovering the holder: a disconnect cannot dodge an already expired fuse.
            game.Tick(now);
            if (game is HotPotato bomb) bomb.RecoverHolder(room.Players.Values.Where(p => p.Connected && game.Players.Contains(p.Id)).Select(p => p.Id).ToArray(), now);
            if (game.FinishedAt is not null && !game.Awarded)
            {
                game.Awarded = true;
                foreach (var result in Results(room))
                {
                    var player = room.Players[result.PlayerId];
                    player.Score += result.Points;
                }
                room.ArchivedRounds[game.Id] = Snapshots.CaptureRound(room, "Completed", Results(room));
                room.ResultsStartedAt = now;
                room.ResultsElapsedMs = 0;
                room.ResultsLeaderboardOpen = false;
                room.LastActivity = now;
            }
            if (game.Phase(now) == "Results" && room.QuickPlay && !room.ResultsLeaderboardOpen && room.HostConnections.Count > 0 && room.Players.Values.Any(p => p.Connected))
            {
                room.NextRoundAt ??= (room.ResultsStartedAt ?? now).AddSeconds(15);
                if (now >= room.NextRoundAt) Start(room, null, true, room.Settings);
            }
            else if (room.HostConnections.Count == 0) room.NextRoundAt = null;
        }
    }
    public void ShowLeaderboard(Room room)
    {
        lock (room.Gate)
        {
            if (room.Game is not { } game || game.Phase(clock.GetUtcNow()) != "Results") throw new PartyException("Leaderboardet kan vises, når runden er slut.");
            var now = clock.GetUtcNow();
            room.ResultsElapsedMs = Math.Clamp((long)(now - (room.ResultsStartedAt ?? now)).TotalMilliseconds, 0, 15000);
            room.ResultsLeaderboardOpen = true;
            room.NextRoundAt = null;
            room.LastActivity = now;
        }
    }
    public void ShowResults(Room room)
    {
        lock (room.Gate)
        {
            if (room.Game is not { } game || game.Phase(clock.GetUtcNow()) != "Results") throw new PartyException("Resultatet kan vises, når runden er slut.");
            var now = clock.GetUtcNow();
            var elapsed = Math.Clamp(room.ResultsElapsedMs, 0, 15000);
            room.ResultsStartedAt = now.AddMilliseconds(-elapsed);
            room.ResultsLeaderboardOpen = false;
            room.NextRoundAt = room.QuickPlay ? now.AddMilliseconds(15000 - elapsed) : null;
            room.LastActivity = now;
        }
    }
    public IReadOnlyList<RankedResult> Results(Room room)
    {
        if (room.Game is not { FinishedAt: not null } game) return [];
        var results = game.GetResults();
        var isEvent = game.Kind is "wheel" or "bomb";
        var rank = 1;
        return results.Select((r, i) =>
        {
            if (i > 0 && (r.Value != results[i - 1].Value || r.Valid != results[i - 1].Valid)) rank = i + 1;
            var winner = !isEvent && r.Valid && rank == 1;
            var bottom = isEvent ? r.Affected : !winner && i >= Math.Max(1, results.Count - 3);
            var points = !isEvent && r.Valid ? Math.Max(0, 4 - rank) : 0;
            return new RankedResult(r.PlayerId, room.Players[r.PlayerId].Name, rank, r.Value, r.Detail, r.Valid, winner, bottom, points);
        }).ToList();
    }
    public object Snapshot(Room room, bool forHost = false)
    {
        var now = clock.GetUtcNow();
        var game = room.Game;
        return new
        {
            code = room.Code, serverNow = now, hostConnected = room.HostConnections.Count > 0, round = room.Round,
            quickPlay = room.QuickPlay, nextRoundAt = room.NextRoundAt, resultsStartedAt = room.ResultsStartedAt, resultsLeaderboardOpen = room.ResultsLeaderboardOpen, resultsElapsedMs = room.ResultsElapsedMs, settings = room.Settings,
            recovered = room.Recovered,
            history = forHost ? room.ArchivedRounds.Values.OrderByDescending(r => r.Round.Number).Take(30).Select(r => new {
                r.Round.Number, r.Round.Kind, r.Round.Status,
                results = r.Results.OrderBy(x => x.Rank).Select(x => new { name = room.Players[x.PlayerId].Name, x.Rank, x.Detail, points = x.Valid && x.Rank is >= 1 and <= 3 ? 4 - x.Rank : 0 }).ToArray()
            }).ToArray() : null,
            players = room.Players.Values.Where(p => !p.Left).Select(p => new { p.Id, p.Name, p.Connected, p.Score, p.Penalties }).ToArray(),
            game = game is null ? null : new { game.Id, game.Kind, phase = game.Phase(now), game.StartsAt,
                participants = game.Players, state = !forHost && game is CookieClicker clicker ? clicker.PlayerRoomState() : game.PublicState(now), results = Results(room) }
        };
    }
}
