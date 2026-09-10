using Gnist.Models;

namespace Gnist.Games;

public sealed class HotPotato(IReadOnlyList<string> players, DateTimeOffset now, int holderIndex, int fuseSeconds)
    : MiniGame("bomb", players, now, fuseSeconds)
{
    public string Holder { get; private set; } = players[holderIndex];
    private DateTimeOffset lastPass;
    private string? previous;
    public void RecoverHolder(IReadOnlyList<string> connected, DateTimeOffset now)
    {
        if (FinishedAt is not null || connected.Contains(Holder) || connected.Count == 0) return;
        Holder = connected[System.Security.Cryptography.RandomNumberGenerator.GetInt32(connected.Count)];
        previous = null;
        lastPass = now;
    }
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "pass" || id != Holder || input.Value == id || !Players.Contains(input.Value ?? "")) return;
        if ((now - lastPass).TotalMilliseconds < 600) return;
        if (Players.Count > 2 && input.Value == previous && (now - lastPass).TotalSeconds < 2) return;
        Record(id, $"pass:{input.Sequence}", input.Value!, now);
        previous = Holder;
        Holder = input.Value!;
        lastPass = now;
    }
    public override object PublicState(DateTimeOffset now) => new { holder = Holder, exploded = FinishedAt is not null };
    public override object PrivateState(string playerId) => new { hasBomb = Holder == playerId };
    public override IReadOnlyList<GameResult> GetResults() => Players.Where(p => p != Holder).Select(p => new GameResult(p, 1, "I sikkerhed"))
        .Append(new(Holder, 0, "Sad med bomben", true, true)).ToList();
}
