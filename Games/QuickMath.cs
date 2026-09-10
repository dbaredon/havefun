using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed record MathProblem(string Expression, int Answer)
{
    public bool Evaluate(string? value) => int.TryParse(value, out var answer) && answer == Answer;
    public static MathProblem Generate()
    {
        var a = RandomNumberGenerator.GetInt32(2, 21); var b = RandomNumberGenerator.GetInt32(2, 13);
        return RandomNumberGenerator.GetInt32(3) switch { 0 => new($"{a} + {b}", a + b), 1 => new($"{Math.Max(a,b)} − {Math.Min(a,b)}", Math.Abs(a-b)), _ => new($"{a%8+2} × {b%8+2}", (a%8+2)*(b%8+2)) };
    }
}

public sealed class QuickMath : MiniGame
{
    private readonly MathProblem[] problems; private readonly Dictionary<string,int> scores = []; private readonly Dictionary<string,double> times = []; private readonly HashSet<string> answered = [];
    private DateTimeOffset questionStartedAt; private int question;
    public QuickMath(IReadOnlyList<string> players, DateTimeOffset now, MathProblem problem) : this(players, now, new[] { problem }) { }
    public QuickMath(IReadOnlyList<string> players, DateTimeOffset now, IReadOnlyList<MathProblem> problems) : base("math", players, now, Math.Max(30, problems.Count * 30)) { this.problems = problems.ToArray(); questionStartedAt = StartsAt; foreach (var p in players) scores[p] = 0; }
    protected override void Update(DateTimeOffset now) { if (now - questionStartedAt >= TimeSpan.FromSeconds(30)) Advance(now); }
    protected override void Input(string id, PlayerInput input, DateTimeOffset now)
    {
        if (input.Action != "answer" || !int.TryParse(input.Value, out _) || !answered.Add(id)) return;
        var correct = problems[question].Evaluate(input.Value); var elapsed = Math.Max(0, (now - questionStartedAt).TotalMilliseconds);
        if (correct) { scores[id]++; times[id] = times.GetValueOrDefault(id) + elapsed; }
        Record(id, $"answer:{question + 1}", input.Value!, now); if (answered.Count == Players.Count) Advance(now);
    }
    private void Advance(DateTimeOffset now) { answered.Clear(); if (++question >= problems.Length) Finish(now); else questionStartedAt = now; }
    public void Skip(DateTimeOffset now) => Advance(now);
    public override object PublicState(DateTimeOffset now) => new { expression = now >= StartsAt ? problems[Math.Min(question, problems.Length - 1)].Expression : null, answer = FinishedAt is not null ? (int?)problems[^1].Answer : null, question = Math.Min(question + 1, problems.Length), totalQuestions = problems.Length, answered = answered.Count, endsAt = EndsAt, questionEndsAt = questionStartedAt.AddSeconds(30) };
    public override object PrivateState(string playerId) => new { submitted = answered.Contains(playerId) };
    public override IReadOnlyList<GameResult> GetResults() => Players.Select(id => new GameResult(id, -scores.GetValueOrDefault(id) * 100000 + times.GetValueOrDefault(id), $"{scores.GetValueOrDefault(id)}/{problems.Length} rigtige", scores.GetValueOrDefault(id) > 0)).OrderByDescending(r => r.Valid).ThenBy(r => r.Value).ToList();
}
