using Gnist.Models;

namespace Gnist.Games;

public sealed class SpinWheel(IReadOnlyList<string> players, DateTimeOffset now, int selectedIndex)
    : MiniGame("wheel", players, now, 7)
{
    protected override void Input(string id, PlayerInput input, DateTimeOffset now) { }
    public override object PublicState(DateTimeOffset now) => new { players = Players, selectedIndex = now >= StartsAt ? (int?)selectedIndex : null, endsAt = EndsAt };
    public override IReadOnlyList<GameResult> GetResults() => [new(Players[selectedIndex], 0, "Hjulet valgte dig!", true, true)];
}
