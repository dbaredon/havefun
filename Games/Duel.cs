using Gnist.Models;

namespace Gnist.Games;

public sealed class Duel(IReadOnlyList<string> players, DateTimeOffset now, IReadOnlyList<string> selected)
    : MiniGame("duel", players, now, 25)
{
    private readonly Dictionary<string, string> choices = [];
    private DateTimeOffset? replayAt;
    public int Attempt { get; private set; } = 1;
    public static int Winner(string first, string second)
    {
        if (!IsChoice(first) || !IsChoice(second)) throw new ArgumentException("Ugyldigt valg");
        if (first == second) return 0;
        return (first, second) is ("rock", "scissors") or ("paper", "rock") or ("scissors", "paper") ? 1 : 2;
    }
    private static bool IsChoice(string? choice) => choice is "rock" or "paper" or "scissors";
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (!selected.Contains(id) || input.Action != $"choose:{Attempt}" || !IsChoice(input.Value) || replayAt is not null) return;
        if (choices.TryAdd(id, input.Value!)) Record(id, $"choose:{Attempt}", input.Value!, now);
        if (choices.Count != 2) return;
        if (Winner(choices[selected[0]], choices[selected[1]]) == 0) replayAt = now.AddSeconds(2);
        else Finish(now);
    }
    protected override void Update(DateTimeOffset now)
    {
        if (replayAt is not { } at || now < at) return;
        choices.Clear();
        replayAt = null;
        Attempt++;
        EndsAt = now.AddSeconds(25);
    }
    public override object PublicState(DateTimeOffset now) => new { selected, attempt = Attempt, tie = replayAt is not null,
        choices = choices.Count == 2 || FinishedAt is not null ? new Dictionary<string, string>(choices) : null, endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { selected = selected.Contains(playerId), submitted = choices.ContainsKey(playerId) };
    public override IReadOnlyList<GameResult> GetResults()
    {
        var winner = choices.Count == 2 ? Winner(choices[selected[0]], choices[selected[1]]) : 0;
        if (winner != 0) return [new(selected[winner - 1], 1, "Vandt duellen"), new(selected[2 - winner], 0, "Tabte duellen", true, true)];
        return selected.Select(id => new GameResult(id, choices.ContainsKey(id) ? 1 : 0,
            choices.ContainsKey(id) ? "Modstanderen svarede ikke" : "Intet valg", choices.ContainsKey(id), !choices.ContainsKey(id))).OrderByDescending(r => r.Value).ToList();
    }
}
