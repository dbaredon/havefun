using System.Text.Json;
using Gnist.Models;

namespace Gnist.Data;

public sealed record RoundSnapshot(StoredRound Round, StoredSubmission[] Submissions, StoredResult[] Results);
public sealed record RoomSnapshot(StoredRoom Room, StoredPlayer[] Players, RoundSnapshot[] Rounds);

public static class Snapshots
{
    public static RoundSnapshot CaptureRound(Room room, string status, IReadOnlyList<RankedResult>? results = null)
    {
        var game = room.Game!;
        return new(new StoredRound {
            Id = game.Id, RoomId = room.Id, Number = room.Round, Kind = game.Kind, Status = status,
            StartsAt = game.StartsAt.ToUnixTimeMilliseconds(), FinishedAt = game.FinishedAt?.ToUnixTimeMilliseconds(),
            StateJson = JsonSerializer.Serialize(game.PublicState(DateTimeOffset.UtcNow))
        }, game.GetSubmissions().Select(s => new StoredSubmission {
            RoundId = game.Id, PlayerId = s.PlayerId, Key = s.Key, Value = s.Value, ReceivedAt = s.ReceivedAt.ToUnixTimeMilliseconds()
        }).ToArray(), (results ?? []).Select(r => new StoredResult {
            RoundId = game.Id, PlayerId = r.PlayerId, Rank = r.Rank, Value = r.Value, Detail = r.Detail,
            Valid = r.Valid, Winner = r.Winner, Bottom = r.Bottom, Consequence = r.Consequence
        }).ToArray());
    }
    public static RoomSnapshot Capture(Room room)
    {
        lock (room.Gate)
        {
            var rounds = new Dictionary<string, RoundSnapshot>(room.ArchivedRounds);
            if (room.Game is { } game && !rounds.ContainsKey(game.Id)) rounds[game.Id] = CaptureRound(room, "Playing");
            return new(new StoredRoom {
                Id = room.Id, Code = room.Code, HostTokenHash = room.HostTokenHash, Revision = ++room.Revision,
                CreatedAt = room.CreatedAt.ToUnixTimeMilliseconds(), LastActivity = room.LastActivity.ToUnixTimeMilliseconds(),
                Closed = room.Closed, RoundNumber = room.Round, PreviousGame = room.PreviousGame, SettingsJson = JsonSerializer.Serialize(room.Settings)
            }, room.Players.Values.Select(p => new StoredPlayer {
                Id = p.Id, RoomId = room.Id, Name = p.Name, TokenHash = p.TokenHash, Score = p.Score, Penalties = p.Penalties, Left = p.Left
            }).ToArray(), rounds.Values.ToArray());
        }
    }
    public static Room Restore(RoomSnapshot snapshot)
    {
        var saved = snapshot.Room;
        var room = new Room(saved.Code, saved.HostTokenHash, DateTimeOffset.FromUnixTimeMilliseconds(saved.CreatedAt), true) {
            Id = saved.Id, Revision = saved.Revision, LastActivity = DateTimeOffset.FromUnixTimeMilliseconds(saved.LastActivity),
            Closed = saved.Closed, Round = saved.RoundNumber, PreviousGame = saved.PreviousGame,
            Settings = JsonSerializer.Deserialize<RoomSettings>(saved.SettingsJson) ?? new()
        };
        foreach (var p in snapshot.Players)
            room.Players.Add(p.Id, new Player(p.Id, p.Name, p.TokenHash, true) { Score = p.Score, Penalties = p.Penalties, Left = p.Left });
        foreach (var round in snapshot.Rounds)
        {
            // Do not resume a timing-sensitive round after downtime. Keep its accepted inputs for history.
            if (round.Round.Status == "Playing") round.Round.Status = "Interrupted";
            room.ArchivedRounds[round.Round.Id] = round;
        }
        room.Recovered = true;
        return room;
    }
}
