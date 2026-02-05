using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiceRollFormulaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dice_type",
                table: "session_tracking_dice_rolls");

            migrationBuilder.DropColumn(
                name: "roll_count",
                table: "session_tracking_dice_rolls");

            migrationBuilder.RenameColumn(
                name: "results",
                table: "session_tracking_dice_rolls",
                newName: "rolls");

            migrationBuilder.RenameIndex(
                name: "idx_dice_session",
                table: "session_tracking_dice_rolls",
                newName: "idx_dice_session_timestamp");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "session_tracking_dice_rolls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "formula",
                table: "session_tracking_dice_rolls",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "session_tracking_dice_rolls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "session_tracking_dice_rolls",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "modifier",
                table: "session_tracking_dice_rolls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total",
                table: "session_tracking_dice_rolls",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "session_tracking_dice_rolls");

            migrationBuilder.DropColumn(
                name: "formula",
                table: "session_tracking_dice_rolls");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "session_tracking_dice_rolls");

            migrationBuilder.DropColumn(
                name: "label",
                table: "session_tracking_dice_rolls");

            migrationBuilder.DropColumn(
                name: "modifier",
                table: "session_tracking_dice_rolls");

            migrationBuilder.DropColumn(
                name: "total",
                table: "session_tracking_dice_rolls");

            migrationBuilder.RenameColumn(
                name: "rolls",
                table: "session_tracking_dice_rolls",
                newName: "results");

            migrationBuilder.RenameIndex(
                name: "idx_dice_session_timestamp",
                table: "session_tracking_dice_rolls",
                newName: "idx_dice_session");

            migrationBuilder.AddColumn<string>(
                name: "dice_type",
                table: "session_tracking_dice_rolls",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "roll_count",
                table: "session_tracking_dice_rolls",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
