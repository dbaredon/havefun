using Gnist.Models;

namespace Gnist.Games;

public sealed class CookieClicker(IReadOnlyList<string> players, DateTimeOffset now, int seconds = 10)
    : MiniGame("cookie", players, now, seconds)
{
    private readonly Dictionary<string, int> counts = players.ToDictionary(p => p, _ => 0);
    private readonly Dictionary<string, long> sequences = [];
    private readonly Dictionary<string, DateTimeOffset> lastTap = [];
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "tap" || input.Sequence <= sequences.GetValueOrDefault(id)) return;
        sequences[id] = input.Sequence;
        // At most 25 accepted taps/sec per player. Never accept a client-supplied click count.
        if (lastTap.TryGetValue(id, out var last) && now - last < TimeSpan.FromMilliseconds(40)) return;
        lastTap[id] = now;
        counts[id]++;
    }
    public override object PublicState(DateTimeOffset now) => new { endsAt = EndsAt, counts = new Dictionary<string, int>(counts) };
    public object PlayerRoomState() => new { endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { count = counts.GetValueOrDefault(playerId) };
    public override IReadOnlyList<GameResult> GetResults() => counts.OrderByDescending(p => p.Value)
        .Select(p => new GameResult(p.Key, p.Value, $"{p.Value} klik")).ToList();
}
