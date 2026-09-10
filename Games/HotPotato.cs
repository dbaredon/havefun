using Gnist.Models;

namespace Gnist.Games;

public sealed class HotPotato(IReadOnlyList<string> players, DateTimeOffset now, int holderIndex, int fuseSeconds)
    : MiniGame("bomb", players, now, fuseSeconds)
{
    public string Holder { get; private set; } = players[holderIndex];
    private DateTimeOffset holderReceivedAt = now.AddSeconds(6);
    public void RecoverHolder(IReadOnlyList<string> connected, DateTimeOffset now)
    {
        if (FinishedAt is not null || connected.Contains(Holder) || connected.Count == 0) return;
        Holder = connected[System.Security.Cryptography.RandomNumberGenerator.GetInt32(connected.Count)];
        holderReceivedAt = now;
    }
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "pass" || id != Holder || input.Value == id || !Players.Contains(input.Value ?? "")) return;
        if ((now - holderReceivedAt).TotalSeconds < 3) return;
        Record(id, $"pass:{input.Sequence}", input.Value!, now);
        Holder = input.Value!;
        holderReceivedAt = now;
    }
    public override object PublicState(DateTimeOffset now) => new { holder = Holder, exploded = FinishedAt is not null };
    public override object PrivateState(string playerId) => new { hasBomb = Holder == playerId };
    public override IReadOnlyList<GameResult> GetResults() => Players.Where(p => p != Holder).Select(p => new GameResult(p, 1, "I sikkerhed"))
        .Append(new(Holder, 0, "Sad med bomben", true, true)).ToList();
}
