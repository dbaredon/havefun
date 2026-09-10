using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gnist.Data.Migrations;

[DbContext(typeof(PartyDbContext))]
public sealed class PartyDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4");
        modelBuilder.Entity("Gnist.Data.StoredRoom", b =>
        {
            b.Property<string>("Id").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Code").IsRequired().HasColumnType("TEXT");
            b.Property<string>("HostTokenHash").IsRequired().HasColumnType("TEXT");
            b.Property<long>("Revision").HasColumnType("BIGINT");
            b.Property<long>("CreatedAt").HasColumnType("BIGINT");
            b.Property<long>("LastActivity").HasColumnType("BIGINT");
            b.Property<bool>("Closed").HasColumnType("BOOLEAN");
            b.Property<int>("RoundNumber").HasColumnType("INTEGER");
            b.Property<string>("PreviousGame").HasColumnType("TEXT");
            b.Property<string>("SettingsJson").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("Rooms");
            b.HasIndex("Code").IsUnique();
        });
        modelBuilder.Entity("Gnist.Data.StoredPlayer", b =>
        {
            b.Property<string>("Id").IsRequired().HasColumnType("TEXT");
            b.Property<string>("RoomId").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Name").IsRequired().HasColumnType("TEXT");
            b.Property<string>("TokenHash").IsRequired().HasColumnType("TEXT");
            b.Property<int>("Score").HasColumnType("INTEGER");
            b.Property<int>("Penalties").HasColumnType("INTEGER");
            b.Property<bool>("Left").HasColumnType("BOOLEAN");
            b.HasKey("Id");
            b.ToTable("Players");
            b.HasIndex("RoomId");
        });
        modelBuilder.Entity("Gnist.Data.StoredRound", b =>
        {
            b.Property<string>("Id").IsRequired().HasColumnType("TEXT");
            b.Property<string>("RoomId").IsRequired().HasColumnType("TEXT");
            b.Property<int>("Number").HasColumnType("INTEGER");
            b.Property<string>("Kind").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Status").IsRequired().HasColumnType("TEXT");
            b.Property<long>("StartsAt").HasColumnType("BIGINT");
            b.Property<long?>("FinishedAt").HasColumnType("BIGINT");
            b.Property<string>("StateJson").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("Rounds");
            b.HasIndex("RoomId");
        });
        modelBuilder.Entity("Gnist.Data.StoredSubmission", b =>
        {
            b.Property<string>("RoundId").IsRequired().HasColumnType("TEXT");
            b.Property<string>("PlayerId").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Key").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Value").IsRequired().HasColumnType("TEXT");
            b.Property<long>("ReceivedAt").HasColumnType("BIGINT");
            b.HasKey("RoundId", "PlayerId", "Key");
            b.ToTable("Submissions");
            b.HasIndex("PlayerId");
        });
        modelBuilder.Entity("Gnist.Data.StoredResult", b =>
        {
            b.Property<string>("RoundId").IsRequired().HasColumnType("TEXT");
            b.Property<string>("PlayerId").IsRequired().HasColumnType("TEXT");
            b.Property<int>("Rank").HasColumnType("INTEGER");
            b.Property<double>("Value").HasColumnType("DOUBLE PRECISION");
            b.Property<string>("Detail").IsRequired().HasColumnType("TEXT");
            b.Property<bool>("Valid").HasColumnType("BOOLEAN");
            b.Property<bool>("Winner").HasColumnType("BOOLEAN");
            b.Property<bool>("Bottom").HasColumnType("BOOLEAN");
            b.Property<string>("Consequence").IsRequired().HasColumnType("TEXT");
            b.HasKey("RoundId", "PlayerId");
            b.ToTable("Results");
            b.HasIndex("PlayerId");
        });
        modelBuilder.Entity("Gnist.Data.StoredPlayer", b =>
        {
            b.HasOne("Gnist.Data.StoredRoom", null).WithMany().HasForeignKey("RoomId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });
        modelBuilder.Entity("Gnist.Data.StoredRound", b =>
        {
            b.HasOne("Gnist.Data.StoredRoom", null).WithMany().HasForeignKey("RoomId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });
        modelBuilder.Entity("Gnist.Data.StoredSubmission", b =>
        {
            b.HasOne("Gnist.Data.StoredRound", null).WithMany().HasForeignKey("RoundId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne("Gnist.Data.StoredPlayer", null).WithMany().HasForeignKey("PlayerId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        modelBuilder.Entity("Gnist.Data.StoredResult", b =>
        {
            b.HasOne("Gnist.Data.StoredRound", null).WithMany().HasForeignKey("RoundId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne("Gnist.Data.StoredPlayer", null).WithMany().HasForeignKey("PlayerId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
    }
}
