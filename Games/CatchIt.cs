using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed class CatchIt(IReadOnlyList<string> players, DateTimeOffset now) : MiniGame("catch", players, now, 60)
{
    private const int TargetCount = 10;
    private readonly Dictionary<string, int> hits = players.ToDictionary(p => p, _ => 0);
    private readonly Dictionary<string, DateTimeOffset> firstHit = [];
    private readonly Dictionary<string, DateTimeOffset> lastHit = [];
    private readonly Dictionary<string, (int X, int Y)> targets = players.ToDictionary(p => p, _ => Position());
    private static (int X, int Y) Position() => (RandomNumberGenerator.GetInt32(8, 88), RandomNumberGenerator.GetInt32(12, 82));
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "hit" || hits[id] >= TargetCount) return;
        firstHit.TryAdd(id, now); lastHit[id] = now; hits[id]++; targets[id] = Position();
        Record(id, $"hit:{hits[id]}", "hit", now);
        if (hits.Values.All(h => h >= TargetCount)) Finish(now);
    }
    public override object PublicState(DateTimeOffset now) => new { hits, targetCount = TargetCount, endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { x = targets[playerId].X, y = targets[playerId].Y, hits = hits[playerId], targetCount = TargetCount };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id =>
        firstHit.TryGetValue(id, out var start) && hits[id] >= TargetCount && lastHit.TryGetValue(id, out var end)
            ? new GameResult(id, (end - start).TotalMilliseconds, $"{(end - start).TotalSeconds:F2} sekunder", true)
            : new GameResult(id, double.MaxValue, $"{hits[id]}/{TargetCount} træffere", false)).OrderByDescending(r => r.Valid).ThenBy(r => r.Value).ToList();
}
