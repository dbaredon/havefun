using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Gnist.Models;
using Microsoft.Extensions.Options;

namespace Gnist.Services;

public sealed class RoomService(IOptions<PartyOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Room> rooms = new();
    private readonly ConcurrentDictionary<string, byte> reservedCodes = new();
    private readonly ConcurrentDictionary<string, (string Code, string? PlayerId)> connections = new();
    private readonly object creationGate = new();
    public IEnumerable<Room> Rooms => rooms.Values.Where(r => !r.Closed);
    public void Restore(Room room)
    {
        if (clock.GetUtcNow() - room.LastActivity >= TimeSpan.FromMinutes(options.Value.RoomIdleMinutes)) room.Closed = true;
        reservedCodes.TryAdd(room.Code, 0);
        if (!room.Closed) rooms.TryAdd(room.Code, room);
    }
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public HostReceipt Create()
    {
        lock (creationGate)
        {
            if (rooms.Values.Count(r => !r.Closed) >= options.Value.MaxRooms) throw new PartyException("Der er fuldt hus lige nu. Prøv igen lidt senere.");
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string code;
            do { code = new string(Enumerable.Range(0, 4).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray()); }
            while (reservedCodes.ContainsKey(code));
            reservedCodes.TryAdd(code, 0);
            var hostToken = Token();
            var room = new Room(code, hostToken, clock.GetUtcNow());
            rooms[code] = room;
            return new(code, hostToken);
        }
    }
    public Room Get(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !rooms.TryGetValue(code.Trim().ToUpperInvariant(), out var room) || room.Closed)
            throw new PartyException("Rummet findes ikke længere. Tjek koden, eller opret en ny fest.");
        return room;
    }
    public void ConnectHost(string code, string token, string connection)
    {
        var room = Get(code);
        lock (room.Gate)
        {
            EnsureOpen(room);
            if (!SessionTokens.Matches(room.HostTokenHash, token)) throw new PartyException("Kun værten kan styre festen. Brug fanen, hvor du oprettede rummet.");
            Bind(connection, room.Code, null);
            room.HostConnections.Add(connection);
            room.LastActivity = clock.GetUtcNow();
        }
    }
    public JoinReceipt Join(string code, string? name, string? token, string connection)
    {
        var room = Get(code);
        lock (room.Gate)
        {
            EnsureOpen(room);
            var player = room.Players.Values.FirstOrDefault(p => SessionTokens.Matches(p.TokenHash, token));
            if (player is null)
            {
                if (room.Players.Values.Count(p => !p.Left) >= options.Value.MaxPlayers) throw new PartyException("Rummet er fyldt. Bed værten om at oprette et nyt rum.");
                name = Regex.Replace(name?.Trim() ?? "", @"\s+", " ");
                if (name.Length is < 1 or > 20 || name.Any(char.IsControl)) throw new PartyException("Skriv et navn på 1–20 tegn.");
                var original = name;
                var suffix = 2;
                while (room.Players.Values.Any(p => !p.Left && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) name = $"{original} {suffix++}";
                token = Token();
                player = new(Guid.NewGuid().ToString("N"), name, token);
                // Bind before mutation so a connection cannot add players to multiple rooms.
                Bind(connection, room.Code, player.Id);
                room.Players.Add(player.Id, player);
            }
            else Bind(connection, room.Code, player.Id);
            player.Left = false;
            player.Connections.Add(connection);
            room.LastActivity = clock.GetUtcNow();
            return new(room.Code, player.Id, token!, player.Name);
        }
    }
    private void Bind(string connection, string code, string? player)
    {
        if (!connections.TryAdd(connection, (code, player)) && connections[connection] != (code, player))
            throw new PartyException("Denne forbindelse er allerede i brug. Åbn en ny fane.");
    }
    public (Room Room, string? PlayerId) Membership(string connection, bool hostOnly = false)
    {
        if (!connections.TryGetValue(connection, out var member)) throw new PartyException("Forbind til rummet igen.");
        if (hostOnly && member.PlayerId is not null) throw new PartyException("Kun værten kan gøre det.");
        return (Get(member.Code), member.PlayerId);
    }
    public Room? Disconnect(string connection, bool leave = false)
    {
        if (!connections.TryRemove(connection, out var member) || !rooms.TryGetValue(member.Code, out var room)) return null;
        lock (room.Gate)
        {
            if (member.PlayerId is null) room.HostConnections.Remove(connection);
            else if (room.Players.TryGetValue(member.PlayerId, out var player))
            {
                player.Connections.Remove(connection);
                if (leave && player.Connections.Count == 0) player.Left = true;
            }
            room.LastActivity = clock.GetUtcNow();
        }
        return room;
    }
    public bool Expire(Room room, DateTimeOffset now)
    {
        lock (room.Gate)
        {
            if (now - room.LastActivity < TimeSpan.FromMinutes(options.Value.RoomIdleMinutes)) return false;
            room.Closed = true;
            rooms.TryRemove(room.Code, out _);

            foreach (var connection in connections.Where(p => p.Value.Code == room.Code)) connections.TryRemove(connection.Key, out _);
            return true;
        }
    }
    public static void EnsureOpen(Room room)
    {
        if (room.Closed) throw new PartyException("Festen er lukket. Opret et nyt rum.");
    }
}
