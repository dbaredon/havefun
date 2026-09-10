using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gnist.Data;

public static class DatabaseSetup
{
    public static string PostgresConnection(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return value;
        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder {
            Host = uri.Host, Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]), Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "",
            SslMode = SslMode.Prefer, IncludeErrorDetail = false
        };
        foreach (var field in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = field.Split('=', 2);
            if (pair[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && pair.Length == 2)
                builder.SslMode = Enum.Parse<SslMode>(pair[1].Replace("-", ""), true);
        }
        return builder.ConnectionString;
    }
    public static IPartyStore Create(IConfiguration config, IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing") || environment.IsEnvironment("Export")) return new MemoryPartyStore();
        var postgres = config.GetConnectionString("PartyDatabase") ?? config["DATABASE_URL"];
        var options = new DbContextOptionsBuilder<PartyDbContext>();
        if (!string.IsNullOrWhiteSpace(postgres)) options.UseNpgsql(PostgresConnection(postgres));
        else if (environment.IsDevelopment())
        {
            var directory = Path.Combine(environment.ContentRootPath, ".local-data");
            Directory.CreateDirectory(directory);
            options.UseSqlite($"Data Source={Path.Combine(directory, "gnist.db")}");
        }
        else throw new InvalidOperationException("Konfigurér DATABASE_URL eller ConnectionStrings__PartyDatabase til PostgreSQL, før spilserveren startes.");
        return new EfPartyStore(options.Options);
    }
}
