using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gnist.Data.Migrations;

[DbContext(typeof(PartyDbContext))]
[Migration("20260910140000_InitialStorage")]
public sealed class InitialStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Rooms" (
                "Id" TEXT NOT NULL PRIMARY KEY, "Code" TEXT NOT NULL, "HostTokenHash" TEXT NOT NULL,
                "Revision" BIGINT NOT NULL, "CreatedAt" BIGINT NOT NULL, "LastActivity" BIGINT NOT NULL,
                "Closed" BOOLEAN NOT NULL, "RoundNumber" INTEGER NOT NULL, "PreviousGame" TEXT NULL, "SettingsJson" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Rooms_Code" ON "Rooms" ("Code");
            CREATE TABLE "Players" (
                "Id" TEXT NOT NULL PRIMARY KEY, "RoomId" TEXT NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
                "Name" TEXT NOT NULL, "TokenHash" TEXT NOT NULL, "Score" INTEGER NOT NULL, "Penalties" INTEGER NOT NULL, "Left" BOOLEAN NOT NULL
            );
            CREATE INDEX "IX_Players_RoomId" ON "Players"("RoomId");
            CREATE TABLE "Rounds" (
                "Id" TEXT NOT NULL PRIMARY KEY, "RoomId" TEXT NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
                "Number" INTEGER NOT NULL, "Kind" TEXT NOT NULL, "Status" TEXT NOT NULL,
                "StartsAt" BIGINT NOT NULL, "FinishedAt" BIGINT NULL, "StateJson" TEXT NOT NULL
            );
            CREATE INDEX "IX_Rounds_RoomId" ON "Rounds"("RoomId");
            CREATE TABLE "Submissions" (
                "RoundId" TEXT NOT NULL REFERENCES "Rounds"("Id") ON DELETE CASCADE,
                "PlayerId" TEXT NOT NULL REFERENCES "Players"("Id"), "Key" TEXT NOT NULL, "Value" TEXT NOT NULL, "ReceivedAt" BIGINT NOT NULL,
                PRIMARY KEY ("RoundId", "PlayerId", "Key")
            );
            CREATE INDEX "IX_Submissions_PlayerId" ON "Submissions"("PlayerId");
            CREATE TABLE "Results" (
                "RoundId" TEXT NOT NULL REFERENCES "Rounds"("Id") ON DELETE CASCADE,
                "PlayerId" TEXT NOT NULL REFERENCES "Players"("Id"), "Rank" INTEGER NOT NULL, "Value" DOUBLE PRECISION NOT NULL,
                "Detail" TEXT NOT NULL, "Valid" BOOLEAN NOT NULL, "Winner" BOOLEAN NOT NULL, "Bottom" BOOLEAN NOT NULL, "Consequence" TEXT NOT NULL,
                PRIMARY KEY ("RoundId", "PlayerId")
            );
            CREATE INDEX "IX_Results_PlayerId" ON "Results"("PlayerId");
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Results");
        migrationBuilder.DropTable("Submissions");
        migrationBuilder.DropTable("Rounds");
        migrationBuilder.DropTable("Players");
        migrationBuilder.DropTable("Rooms");
    }
}
