using Microsoft.EntityFrameworkCore;

namespace Gnist.Data;

public sealed class PartyDbContext(DbContextOptions<PartyDbContext> options) : DbContext(options)
{
    public DbSet<StoredRoom> Rooms => Set<StoredRoom>();
    public DbSet<StoredPlayer> Players => Set<StoredPlayer>();
    public DbSet<StoredRound> Rounds => Set<StoredRound>();
    public DbSet<StoredSubmission> Submissions => Set<StoredSubmission>();
    public DbSet<StoredResult> Results => Set<StoredResult>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<StoredRoom>().ToTable("Rooms").HasKey(x => x.Id);
        model.Entity<StoredRoom>().HasIndex(x => x.Code).IsUnique();
        model.Entity<StoredPlayer>().ToTable("Players").HasKey(x => x.Id);
        model.Entity<StoredPlayer>().HasOne<StoredRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<StoredRound>().ToTable("Rounds").HasKey(x => x.Id);
        model.Entity<StoredRound>().HasOne<StoredRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<StoredSubmission>().ToTable("Submissions").HasKey(x => new { x.RoundId, x.PlayerId, x.Key });
        model.Entity<StoredSubmission>().HasOne<StoredRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<StoredSubmission>().HasOne<StoredPlayer>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<StoredResult>().ToTable("Results").HasKey(x => new { x.RoundId, x.PlayerId });
        model.Entity<StoredResult>().HasOne<StoredRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<StoredResult>().HasOne<StoredPlayer>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                property.SetColumnType(type == typeof(string) ? "TEXT" : type == typeof(long) ? "BIGINT" :
                    type == typeof(int) ? "INTEGER" : type == typeof(bool) ? "BOOLEAN" : "DOUBLE PRECISION");
            }
    }
}
public sealed class StoredRoom
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string HostTokenHash { get; set; } = "";
    public long Revision { get; set; }
    public long CreatedAt { get; set; }
    public long LastActivity { get; set; }
    public bool Closed { get; set; }
    public int RoundNumber { get; set; }
    public string? PreviousGame { get; set; }
    public string SettingsJson { get; set; } = "{}";
}
public sealed class StoredPlayer
{
    public string Id { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public int Score { get; set; }
    public int Penalties { get; set; }
    public bool Left { get; set; }
}
public sealed class StoredRound
{
    public string Id { get; set; } = "";
    public string RoomId { get; set; } = "";
    public int Number { get; set; }
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "Playing";
    public long StartsAt { get; set; }
    public long? FinishedAt { get; set; }
    public string StateJson { get; set; } = "{}";
}
public sealed class StoredSubmission
{
    public string RoundId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public long ReceivedAt { get; set; }
}
public sealed class StoredResult
{
    public string RoundId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public int Rank { get; set; }
    public double Value { get; set; }
    public string Detail { get; set; } = "";
    public bool Valid { get; set; }
    public bool Winner { get; set; }
    public bool Bottom { get; set; }
    public string Consequence { get; set; } = "";
}
