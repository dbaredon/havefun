using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed record MathProblem(string Expression, int Answer)
{
    public bool Evaluate(string? value) => int.TryParse(value, out var answer) && answer == Answer;
    public static MathProblem Generate()
    {
        var a = RandomNumberGenerator.GetInt32(2, 21);
        var b = RandomNumberGenerator.GetInt32(2, 13);
        return RandomNumberGenerator.GetInt32(3) switch
        {
            0 => new($"{a} + {b}", a + b),
            1 => new($"{Math.Max(a, b)} − {Math.Min(a, b)}", Math.Abs(a - b)),
            _ => new($"{a % 8 + 2} × {b % 8 + 2}", (a % 8 + 2) * (b % 8 + 2))
        };
    }
}

public sealed class QuickMath(IReadOnlyList<string> players, DateTimeOffset now, MathProblem problem)
    : MiniGame("math", players, now, 20)
{
    private readonly Dictionary<string, GameResult> answers = [];
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "answer" || !int.TryParse(input.Value, out _)) return;
        var correct = problem.Evaluate(input.Value);
        var ms = (now - StartsAt).TotalMilliseconds;
        answers.TryAdd(id, new(id, ms, correct ? $"Rigtigt · {ms / 1000:F2} s" : "Forkert svar", correct));
        if (answers.Count == Players.Count) Finish(now);
    }
    public override object PublicState(DateTimeOffset now) => new { expression = now >= StartsAt ? problem.Expression : null,
        answer = FinishedAt is not null ? (int?)problem.Answer : null, answered = answers.Count, endsAt = EndsAt };
    public override object PrivateState(string playerId) => new { submitted = answers.ContainsKey(playerId) };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id => answers.GetValueOrDefault(id)
        ?? new GameResult(id, double.MaxValue, "Intet svar", false)).OrderByDescending(r => r.Valid).ThenBy(r => r.Value).ToList();
}
