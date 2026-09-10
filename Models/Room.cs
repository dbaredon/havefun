namespace Gnist.Models;

public sealed class PartyOptions
{
    public string Brand { get; set; } = "GNIST";
    public int RoomIdleMinutes { get; set; } = 120;
    public int MaxPlayers { get; set; } = 100;
    public int MaxRooms { get; set; } = 500;
    public string PublicBaseUrl { get; set; } = "";
}

public sealed class Player(string id, string name, string token)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Token { get; } = token;
    public HashSet<string> Connections { get; } = [];
    public bool Connected => Connections.Count > 0;
    public int Score { get; set; }
    public int Penalties { get; set; }
}

public sealed class Room(string code, string hostToken, DateTimeOffset now)
{
    public object Gate { get; } = new();
    public string Code { get; } = code;
    public string HostToken { get; } = hostToken;
    public HashSet<string> HostConnections { get; } = [];
    public Dictionary<string, Player> Players { get; } = [];
    public Games.MiniGame? Game { get; set; }
    public string? PreviousGame { get; set; }
    public int Round { get; set; }
    public bool QuickPlay { get; set; }
    public DateTimeOffset? NextRoundAt { get; set; }
    public DateTimeOffset LastActivity { get; set; } = now;
    public bool Closed { get; set; }
    public RoomSettings Settings { get; set; } = new();
}

public sealed record RoomSettings(int ClickSeconds = 10, string Consequence = "none", string ConsequenceText = "", int PenaltyPoints = 1);
public sealed record JoinReceipt(string Code, string PlayerId, string PlayerToken, string Name);
public sealed record HostReceipt(string Code, string HostToken);
public sealed record PlayerInput(string RoundId, string Action, string? Value, long Sequence);
public sealed record GameResult(string PlayerId, double Value, string Detail, bool Valid = true, bool Affected = false);
public sealed record RankedResult(string PlayerId, string Name, int Rank, double Value, string Detail, bool Valid, bool Winner, bool Bottom, string Consequence);
public sealed record GameInfo(string Id, string Name, string Description, string Icon, string Category);
public sealed class PartyException(string message) : Exception(message);
