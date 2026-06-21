using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageChunkCitationsJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_message_chunk_citations",
                columns: table => new
                {
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_message_chunk_citations", x => new { x.ThreadId, x.MessageId, x.ChunkId });
                    table.ForeignKey(
                        name: "FK_chat_message_chunk_citations_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_message_chunk_citations_text_chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalTable: "text_chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_chunk_citations_ChunkId",
                table: "chat_message_chunk_citations",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_msg_chunk_thread_chunk",
                table: "chat_message_chunk_citations",
                columns: new[] { "ThreadId", "ChunkId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_message_chunk_citations");
        }
    }
}
