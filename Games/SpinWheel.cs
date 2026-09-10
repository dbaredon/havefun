using Gnist.Models;

namespace Gnist.Games;

public sealed class SpinWheel : MiniGame
{
    private readonly string[] selected;
    public SpinWheel(IReadOnlyList<string> players, DateTimeOffset now, int selectedIndex) : this(players, now, new[] { players[selectedIndex] }) { }
    public SpinWheel(IReadOnlyList<string> players, DateTimeOffset now, int first, int second) : this(players, now, new[] { players[first], players[second] }) { }
    private SpinWheel(IReadOnlyList<string> players, DateTimeOffset now, string[] selected) : base("wheel", players, now, selected.Length > 1 ? 14 : 7) => this.selected = selected.Distinct().ToArray();
    protected override void Input(string id, PlayerInput input, DateTimeOffset now) { }
    public override object PublicState(DateTimeOffset now)
    {
        var spin = now < StartsAt ? -1 : Math.Min(selected.Length - 1, (int)((now - StartsAt).TotalSeconds / 7));
        var selectedIndex = spin < 0 ? (int?)null : Array.IndexOf(Players.ToArray(), selected[spin]);
        return new { players = Players, selectedIndex, spin = spin + 1, spins = selected.Length, endsAt = EndsAt };
    }
    public override IReadOnlyList<GameResult> GetResults() => selected.Select(id => new GameResult(id, 0, "Hjulet valgte dig!", true, true)).ToList();
}
