using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationSummaryToChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationSummary",
                table: "ChatThreads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSummarizedMessageCount",
                table: "ChatThreads",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSummarizedMessageCount",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "ConversationSummary",
                table: "ChatThreads");
        }
    }
}
