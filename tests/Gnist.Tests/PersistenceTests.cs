using Gnist.Data;
using Gnist.Games;
using Gnist.Models;
using Gnist.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Gnist.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task SqliteMigratesAndRestoresNamesTokensAnswersPointsAndHistory()
    {
        var file = Path.Combine(Path.GetTempPath(), $"gnist-test-{Guid.NewGuid():N}.db");
        try { await VerifyRoundTrip(new DbContextOptionsBuilder<PartyDbContext>().UseSqlite($"Data Source={file};Pooling=False").Options); }
        finally { File.Delete(file); }
    }
    [Fact]
    public void PostgresSchemaHasNoPendingChangesAndGeneratesMigrationSql()
    {
        using var db = new PartyDbContext(new DbContextOptionsBuilder<PartyDbContext>().UseNpgsql("Host=localhost;Database=gnist_tests;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE \"Rooms\"", sql);
        Assert.Contains("CREATE TABLE \"Submissions\"", sql);
        Assert.Contains("CREATE TABLE \"Results\"", sql);
    }
    [Fact]
    public async Task StaleSnapshotsNeverOverwriteNewerTotals()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = new EfPartyStore(new DbContextOptionsBuilder<PartyDbContext>().UseSqlite(connection).Options);
        await store.InitializeAsync();
        var room = new Room("X7K2", "host", DateTimeOffset.UtcNow);
        room.Players["p"] = new("p", "Tony", "token");
        var old = Snapshots.Capture(room);
        room.Players["p"].Score = 9;
        var newer = Snapshots.Capture(room);
        await store.SaveAsync(newer);await store.SaveAsync(old);await store.SaveAsync(newer);
        Assert.Equal(9,Assert.Single(Assert.Single(await store.LoadAsync()).Players).Score);
    }
    [Fact]
    public void RenderDatabaseUrlsAreParsedWithoutLosingEscapedCredentials()
    {
        var value = new NpgsqlConnectionStringBuilder(DatabaseSetup.PostgresConnection("postgresql://user:p%40ss%3Aword@database.internal:5432/gnist?sslmode=require"));
        Assert.Equal("p@ss:word",value.Password);Assert.Equal("gnist",value.Database);Assert.Equal(SslMode.Require,value.SslMode);
        Assert.Equal("Host=localhost",DatabaseSetup.PostgresConnection("Host=localhost"));
    }
    [PostgresFact]
    public async Task RealPostgresMigrationAndRestartRoundTrip()
    {
        var adminSettings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GNIST_TEST_POSTGRES"));
        var database = "gnist_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminSettings.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"",admin)) await create.ExecuteNonQueryAsync();
        try
        {
            adminSettings.Database = database;adminSettings.Pooling = false;
            await VerifyRoundTrip(new DbContextOptionsBuilder<PartyDbContext>().UseNpgsql(adminSettings.ConnectionString).Options);
        }
        finally { await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\"",admin);await drop.ExecuteNonQueryAsync(); }
    }
    private static async Task VerifyRoundTrip(DbContextOptions<PartyDbContext> options)
    {
        var store = new EfPartyStore(options);
        await store.InitializeAsync();await store.InitializeAsync(); // Migration is repeatable.
        var clock = new ManualClock();
        var service = new RoomService(Options.Create(new PartyOptions()),clock);
        var host = service.Create();
        var a = service.Join(host.Code,"Tony",null,"a");var b = service.Join(host.Code,"Emma",null,"b");
        var room = service.Get(host.Code);
        var manager = new GameManager(new GameCatalog(),clock);
        manager.Start(room,"math",false,new());
        // Use a known server-generated problem for deterministic answer storage.
        room.Game = new QuickMath([a.PlayerId,b.PlayerId],clock.Now,new MathProblem("7 + 13",20));
        clock.Now = room.Game.StartsAt.AddSeconds(1);
        manager.Input(room,a.PlayerId,new(room.Game.Id,"answer","20",1));
        manager.Input(room,a.PlayerId,new(room.Game.Id,"answer","99",2));
        clock.Now = clock.Now.AddSeconds(1);
        manager.Input(room,b.PlayerId,new(room.Game.Id,"answer","19",1));
        var snapshot = Snapshots.Capture(room);
        await store.SaveAsync(snapshot);
        var saved = Assert.Single(await new EfPartyStore(options).LoadAsync());
        Assert.NotEqual(host.HostToken,saved.Room.HostTokenHash);
        Assert.NotEqual(a.PlayerToken,saved.Players.Single(p=>p.Id==a.PlayerId).TokenHash);
        var round = Assert.Single(saved.Rounds);
        Assert.Equal("Completed",round.Round.Status);
        Assert.Equal("20",round.Submissions.Single(s=>s.PlayerId==a.PlayerId).Value);
        Assert.Equal(2,round.Submissions.Length);
        Assert.Equal(3,saved.Players.Single(p=>p.Id==a.PlayerId).Score);
        Assert.True(round.Results.Single(r=>r.PlayerId==a.PlayerId).Winner);
        var restored = Snapshots.Restore(saved);
        var restarted = new RoomService(Options.Create(new PartyOptions()),clock);
        restarted.Restore(restored);
        restarted.ConnectHost(host.Code,host.HostToken,"new-host");
        var player = restarted.Join(host.Code,"Other",a.PlayerToken,"new-a");
        Assert.Equal(a.PlayerId,player.PlayerId);Assert.Equal("Tony",player.Name);
        Assert.Equal(a.PlayerToken,player.PlayerToken);Assert.Null(restored.Game);
        Assert.Equal(3,restored.Players[a.PlayerId].Score);
        Assert.Single(restored.ArchivedRounds);
        manager.Start(restored,"timing",false,new());
        clock.Now = restored.Game!.StartsAt.AddSeconds(1);
        manager.Input(restored,a.PlayerId,new(restored.Game.Id,"start",null,1));
        await store.SaveAsync(Snapshots.Capture(restored));
        var interrupted = Snapshots.Restore(Assert.Single(await store.LoadAsync()));
        Assert.Contains(interrupted.ArchivedRounds.Values,r=>r.Round.Status=="Interrupted");
        Assert.Equal(3,interrupted.Players[a.PlayerId].Score);
        Assert.False(interrupted.QuickPlay);
    }
}
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GNIST_TEST_POSTGRES")))
            Skip = "Set GNIST_TEST_POSTGRES to test a disposable PostgreSQL database. CI runs this test.";
    }
}
