using Gnist.Models;

namespace Gnist.Games;

public sealed class Reaction : MiniGame
{
    private readonly DateTimeOffset goAt;
    private readonly Dictionary<string, double> responses = [];
    public Reaction(IReadOnlyList<string> players, DateTimeOffset now, int delayMs) : base("reaction", players, now, 15)
    {
        goAt = StartsAt.AddMilliseconds(delayMs);
        EndsAt = goAt.AddSeconds(8);
    }
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "react") return;
        if (responses.TryAdd(id, now < goAt ? -1 : (now - goAt).TotalMilliseconds))
            Record(id, "reactionMs", responses[id].ToString(System.Globalization.CultureInfo.InvariantCulture), now);
        if (responses.Count == Players.Count) Finish(now);
    }
    // The randomized GO time must never be sent in advance.
    public override object PublicState(DateTimeOffset now) => new { go = now >= goAt, answered = responses.Count };
    public override object PrivateState(string playerId) => new { submitted = responses.ContainsKey(playerId), falseStart = responses.GetValueOrDefault(playerId) < 0 };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id => responses.TryGetValue(id, out var ms)
        ? new GameResult(id, ms < 0 ? double.MaxValue : ms, ms < 0 ? "Tyvstart" : $"{ms:F0} ms", ms >= 0)
        : new GameResult(id, double.MaxValue, "Intet svar", false)).OrderBy(r => r.Value).ToList();
}
