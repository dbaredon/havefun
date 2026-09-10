using Gnist.Models;

namespace Gnist.Games;

public sealed class PerfectTiming(IReadOnlyList<string> players, DateTimeOffset now, int target)
    : MiniGame("timing", players, now, target + 20)
{
    private readonly Dictionary<string, DateTimeOffset> starts = [];
    private readonly Dictionary<string, double> elapsed = [];
    public static double Difference(double milliseconds, int targetSeconds) => Math.Abs(milliseconds - targetSeconds * 1000);
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action == "start") starts.TryAdd(id, now);
        if (input.Action == "stop" && starts.TryGetValue(id, out var start)) elapsed.TryAdd(id, (now - start).TotalMilliseconds);
        if (elapsed.Count == Players.Count) Finish(now);
    }
    public override object PublicState(DateTimeOffset now) => new { target, answered = elapsed.Count, endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { started = starts.ContainsKey(playerId), submitted = elapsed.ContainsKey(playerId) };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id => elapsed.TryGetValue(id, out var ms)
        ? new GameResult(id, Difference(ms, target), $"{ms / 1000:F3} s · {Difference(ms, target) / 1000:F3} s fra målet")
        : new GameResult(id, double.MaxValue, "Ingen tid", false)).OrderBy(r => r.Value).ToList();
}
