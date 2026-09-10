using System.Security.Cryptography;
using Gnist.Models;

namespace Gnist.Games;

public sealed class GameCatalog
{
    public static readonly GameInfo[] All = [
        new("cookie", "Klikamok", "10–60 sekunder. Én knap. Giv den alt, du har.", "◉", "HURTIGE FINGRE"),
        new("timing", "På sekundet", "Find dit indre ur. Stop så tæt på målet som muligt.", "◷", "MAVEFORNEMMELSE"),
        new("reaction", "Lynhurtig", "Vent på NU. Tryk før de andre. Ingen tyvstart.", "ϟ", "REFLEKSER"),
        new("math", "Hovedbrud", "En lille udregning. Et stort tidspres.", "+", "HURTIGE HOVEDER"),
        new("duel", "Duellen", "To spillere. Sten, saks, papir. Én vinder.", "⚔", "ÉN MOD ÉN"),
        new("wheel", "Skæbnehjulet", "Alle er med. Hjulet bestemmer, hvem det bliver.", "✳", "REN TILFÆLDIGHED"),
        new("bomb", "Tikkende bombe", "Send den videre, før tiden løber ud.", "✹", "VARME HÆNDER")
    ];
    public MiniGame Create(string kind, IReadOnlyList<string> players, DateTimeOffset now, RoomSettings settings) => kind switch
    {
        "cookie" => new CookieClicker(players, now, settings.ClickSeconds),
        "timing" => new PerfectTiming(players, now, new[] { 3, 5, 7, 10 }[RandomNumberGenerator.GetInt32(4)]),
        "reaction" => new Reaction(players, now, RandomNumberGenerator.GetInt32(1800, 5001)),
        "math" => new QuickMath(players, now, MathProblem.Generate()),
        "duel" when players.Count >= 2 => new Duel(players, now, PickDuelists(players)),
        "wheel" => new SpinWheel(players, now, RandomNumberGenerator.GetInt32(players.Count)),
        "bomb" when players.Count >= 2 => new HotPotato(players, now, RandomNumberGenerator.GetInt32(players.Count), RandomNumberGenerator.GetInt32(60, 181)),
        _ => throw new PartyException("Vælg et spil, og sørg for, at mindst to spillere er med.")
    };
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
}
