using Gnist.Models;

namespace Gnist.Games;

public interface IMiniGame
{
    void Tick(DateTimeOffset now);
    void HandleInput(string playerId, PlayerInput input, DateTimeOffset now);
    object PublicState(DateTimeOffset now);
    object PrivateState(string playerId);
    IReadOnlyList<GameResult> GetResults();
}

// All calls are serialized by the owning room's lock. Only explicit projections leave the server.
public abstract class MiniGame : IMiniGame
{
    protected MiniGame(string kind, IReadOnlyList<string> players, DateTimeOffset now, int seconds)
    {
        Kind = kind;
        Players = players;
        StartsAt = now.AddSeconds(6);
        EndsAt = StartsAt.AddSeconds(seconds);
    }
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Kind { get; }
    public IReadOnlyList<string> Players { get; }
    public DateTimeOffset StartsAt { get; }
    protected DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public bool Awarded { get; set; }
    public string Phase(DateTimeOffset now) => FinishedAt is { } end ? (now < end.AddSeconds(1) ? "Finished" : "Results")
        : now < StartsAt.AddSeconds(-3) ? "Intro" : now < StartsAt ? "Countdown" : "Playing";
    public void Tick(DateTimeOffset now)
    {
        if (FinishedAt is not null || now < StartsAt) return;
        Update(now);
        if (now >= EndsAt) Finish(now);
    }
    protected virtual void Update(DateTimeOffset now) { }
    protected void Finish(DateTimeOffset now) => FinishedAt ??= now;
    public void HandleInput(string playerId, PlayerInput input, DateTimeOffset now)
    {
        Tick(now);
        if (input.RoundId != Id || !Players.Contains(playerId) || Phase(now) != "Playing") return;
        Input(playerId, input, now);
    }
    protected abstract void Input(string id, PlayerInput input, DateTimeOffset now);
    public abstract object PublicState(DateTimeOffset now);
    public virtual object PrivateState(string playerId) => new { };
    public abstract IReadOnlyList<GameResult> GetResults();
    protected object TimedState(DateTimeOffset now, object data) => new { endsAt = EndsAt, data };
}
