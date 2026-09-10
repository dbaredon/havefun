using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed class GameCatalog
{
    public static readonly GameInfo[] All = [
        new("cookie", "Klikamok", "10–45 sekunder. Én knap. Giv den alt, du har.", "◉", "HURTIGE FINGRE"),
        new("timing", "På sekundet", "Find dit indre ur. Stop så tæt på målet som muligt.", "◷", "MAVEFORNEMMELSE"),
        new("reaction", "Lynhurtig", "Vent på NU. Tryk før de andre. Ingen tyvstart.", "ϟ", "REFLEKSER"),
        new("math", "Hovedbrud", "En lille udregning. Et stort tidspres.", "+", "HURTIGE HOVEDER"),
        new("pattern", "Tal mønster", "Hvad er næste tal i rækken? Find mønsteret.", "#", "LOGIK"),
        new("catch", "Fang den", "Ram cirklen 10 gange så hurtigt som muligt.", "●", "HURTIGE FINGRE"),
        new("duel", "Duellen", "To spillere. Sten, saks, papir. Én vinder.", "⚔", "ÉN MOD ÉN"),
        new("wheel", "Skæbnehjulet", "Alle er med. Hjulet bestemmer, hvem det bliver.", "✳", "REN TILFÆLDIGHED"),
        new("bomb", "Tikkende bombe", "Send den videre, før tiden løber ud.", "✹", "VARME HÆNDER")
    ];
    public MiniGame Create(string kind, IReadOnlyList<string> players, DateTimeOffset now, RoomSettings settings) => kind switch
    {
        "cookie" => new CookieClicker(players, now, RandomNumberGenerator.GetInt32(10, 46)),
        "timing" => new PerfectTiming(players, now, RandomNumberGenerator.GetInt32(100, 6001) / 100.0),
        "reaction" => new Reaction(players, now, RandomNumberGenerator.GetInt32(1800, 5001)),
        "math" => new QuickMath(players, now, Enumerable.Range(0, 5).Select(_ => MathProblem.Generate()).ToArray()),
        "pattern" => new NumberPattern(players, now),
        "catch" => new CatchIt(players, now),
        "duel" when players.Count >= 2 => new Duel(players, now, PickDuelists(players)),
        "wheel" when players.Count >= 2 => CreateWheel(players, now),
        "wheel" => new SpinWheel(players, now, 0),
        "bomb" when players.Count >= 2 => new HotPotato(players, now, RandomNumberGenerator.GetInt32(players.Count), RandomNumberGenerator.GetInt32(30, 91)),
        _ => throw new PartyException("Vælg et spil, og sørg for, at mindst to spillere er med.")
    };
    private static SpinWheel CreateWheel(IReadOnlyList<string> players, DateTimeOffset now)
    {
        var indices = Enumerable.Range(0, players.Count).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Take(2).ToArray();
        return new SpinWheel(players, now, indices[0], indices[1]);
    }
    private static string[] PickDuelists(IReadOnlyList<string> players)
    {
        var shuffled = players.ToArray();
        RandomNumberGenerator.Shuffle(shuffled.AsSpan());
        return shuffled[..2];
    }
    public string RandomNext(string? previous, int playerCount)
    {
        var choices = All.Where(g => g.Id != previous && (playerCount >= 2 || g.Id is not ("duel" or "bomb"))).ToArray();
        return choices[RandomNumberGenerator.GetInt32(choices.Length)].Id;
    }

    public string RandomNext(string? previous, int playerCount, IEnumerable<string> played)
    {
        var eligible = All.Where(g => playerCount >= 2 || g.Id is not ("duel" or "bomb")).ToArray();
        var used = played.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = eligible.Where(g => !used.Contains(g.Id) && g.Id != previous).ToArray();
        if (choices.Length == 0) choices = eligible.Where(g => !used.Contains(g.Id)).ToArray();
        if (choices.Length == 0) choices = eligible.Where(g => g.Id != previous).ToArray();
        return choices[RandomNumberGenerator.GetInt32(choices.Length)].Id;
    }
}
