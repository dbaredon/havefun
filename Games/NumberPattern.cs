using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed class NumberPattern(IReadOnlyList<string> players, DateTimeOffset now) : MiniGame("pattern", players, now, 20)
{
    private readonly int start = RandomNumberGenerator.GetInt32(-40, 41);
    private readonly int step = RandomNumberGenerator.GetInt32(1, 16) * (RandomNumberGenerator.GetInt32(2) == 0 ? -1 : 1);
    private readonly int missing = RandomNumberGenerator.GetInt32(5);
    private readonly Dictionary<string, GameResult> answers = [];
    private int Answer => start + step * missing;
    private int[] Sequence => Enumerable.Range(0, 5).Select(i => start + step * i).ToArray();
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "answer" || !int.TryParse(input.Value, out var answer)) return;
        var ms = Math.Max(0, (now - StartsAt).TotalMilliseconds);
        if (answers.TryAdd(id, new(id, ms, answer == Answer ? $"Rigtigt · {ms / 1000:F2} s" : "Forkert svar", answer == Answer)))
            Record(id, "answer", input.Value!, now);
        if (answers.Count == Players.Count) Finish(now);
    }
    public override object PublicState(DateTimeOffset now) => new { sequence = now >= StartsAt ? Sequence.Select((n, i) => i == missing ? (int?)null : n).ToArray() : null, answer = FinishedAt is not null ? (int?)Answer : null, answered = answers.Count, endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { submitted = answers.ContainsKey(playerId) };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id => answers.GetValueOrDefault(id) ?? new GameResult(id, double.MaxValue, "Intet svar", false)).OrderByDescending(r => r.Valid).ThenBy(r => r.Value).ToList();
}
