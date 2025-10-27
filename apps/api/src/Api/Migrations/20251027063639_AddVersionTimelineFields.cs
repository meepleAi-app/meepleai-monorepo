using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionTimelineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MergedFromVersionIds",
                table: "rule_specs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentVersionId",
                table: "rule_specs",
                type: "uuid",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_ParentVersionId",
                table: "rule_specs",
                column: "ParentVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_rule_specs_ParentVersionId",
                table: "rule_specs",
                column: "ParentVersionId",
                principalTable: "rule_specs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_rule_specs_ParentVersionId",
                table: "rule_specs");

            migrationBuilder.DropIndex(
                name: "IX_rule_specs_ParentVersionId",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "MergedFromVersionIds",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "ParentVersionId",
                table: "rule_specs");
        }
    }
}
