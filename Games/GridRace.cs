using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed class GridRace(IReadOnlyList<string> players, DateTimeOffset now) : MiniGame("grid", players, now, 60)
{
    private readonly Dictionary<string, int[]> grids = players.ToDictionary(p => p, _ => Shuffled());
    private readonly Dictionary<string, int> next = players.ToDictionary(p => p, _ => 1);
    private readonly Dictionary<string, DateTimeOffset> completed = [];
    private static int[] Shuffled() { var values = Enumerable.Range(1, 9).ToArray(); RandomNumberGenerator.Shuffle(values.AsSpan()); return values; }
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "grid" || !int.TryParse(input.Value, out var value) || value is < 1 or > 9 || completed.ContainsKey(id)) return;
        if (value != next[id]) { grids[id] = Shuffled(); next[id] = 1; Record(id, $"reset:{now.Ticks}", value.ToString(), now); return; }
        next[id]++; Record(id, $"press:{value}", value.ToString(), now);
        if (next[id] == 10) { completed[id] = now; if (completed.Count == Players.Count) Finish(now); }
    }
    public override object PublicState(DateTimeOffset now) => new { progress = next.ToDictionary(x => x.Key, x => x.Value - 1), target = 9, endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { grid = grids[playerId], next = next[playerId], completed = completed.ContainsKey(playerId) };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id => completed.TryGetValue(id, out var at) ? new GameResult(id, (at - StartsAt).TotalMilliseconds, $"{(at - StartsAt).TotalSeconds:F2} sekunder", true) : new GameResult(id, double.MaxValue, $"{next[id] - 1}/9 rigtige", false)).OrderByDescending(r => r.Valid).ThenBy(r => r.Value).ToList();
}
