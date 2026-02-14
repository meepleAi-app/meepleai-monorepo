using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceForecasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resource_forecasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GrowthPattern = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MonthlyGrowthRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CurrentUsers = table.Column<int>(type: "integer", nullable: false),
                    CurrentDbSizeGb = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    CurrentDailyTokens = table.Column<long>(type: "bigint", nullable: false),
                    CurrentCacheMb = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    CurrentVectorEntries = table.Column<long>(type: "bigint", nullable: false),
                    DbPerUserMb = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    TokensPerUserPerDay = table.Column<int>(type: "integer", nullable: false),
                    CachePerUserMb = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    VectorsPerUser = table.Column<int>(type: "integer", nullable: false),
                    ProjectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProjectedMonthlyCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_forecasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceForecasts_CreatedAt",
                table: "resource_forecasts",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceForecasts_CreatedByUserId",
                table: "resource_forecasts",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_forecasts");
        }
    }
}
