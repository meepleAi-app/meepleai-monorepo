using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialGlobalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_request_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Query = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ResponseSnippet = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FinishReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_request_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Resource = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Details = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "n8n_configs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestResult = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_n8n_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_n8n_configs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdf_documents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    CharacterCount = table.Column<int>(type: "integer", nullable: true),
                    ProcessingError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExtractedTables = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    ExtractedDiagrams = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    AtomicRules = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    TableCount = table.Column<int>(type: "integer", nullable: true),
                    DiagramCount = table.Column<int>(type: "integer", nullable: true),
                    AtomicRuleCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pdf_documents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pdf_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rule_specs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_specs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_specs_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rule_specs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AgentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chats_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chats_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_logs_chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rule_atoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleSpecId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Section = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_atoms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_atoms_rule_specs_RuleSpecId",
                        column: x => x.RuleSpecId,
                        principalTable: "rule_specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vector_documents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfDocumentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCharacters = table.Column<int>(type: "integer", nullable: false),
                    IndexingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IndexingError = table.Column<string>(type: "text", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vector_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vector_documents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vector_documents_pdf_documents_PdfDocumentId",
                        column: x => x.PdfDocumentId,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId",
                table: "agents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_Name",
                table: "agents",
                columns: new[] { "GameId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_CreatedAt",
                table: "ai_request_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_Endpoint",
                table: "ai_request_logs",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_GameId",
                table: "ai_request_logs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_UserId",
                table: "ai_request_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_ChatId",
                table: "chat_logs",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_ChatId_CreatedAt",
                table: "chat_logs",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chats_AgentId",
                table: "chats",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_chats_GameId",
                table: "chats",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_chats_GameId_StartedAt",
                table: "chats",
                columns: new[] { "GameId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chats_UserId",
                table: "chats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chats_UserId_LastMessageAt",
                table: "chats",
                columns: new[] { "UserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_games_Name",
                table: "games",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_CreatedByUserId",
                table: "n8n_configs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_Name",
                table: "n8n_configs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId",
                table: "pdf_documents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_UploadedByUserId",
                table: "pdf_documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "GameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_atoms_RuleSpecId",
                table: "rule_atoms",
                column: "RuleSpecId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_atoms_RuleSpecId_SortOrder",
                table: "rule_atoms",
                columns: new[] { "RuleSpecId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_CreatedByUserId",
                table: "rule_specs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameId",
                table: "rule_specs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameId_Version",
                table: "rule_specs",
                columns: new[] { "GameId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_TokenHash",
                table: "user_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId",
                table: "user_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_GameId",
                table: "vector_documents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_PdfDocumentId",
                table: "vector_documents",
                column: "PdfDocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_logs");

            migrationBuilder.DropTable(
                name: "rule_atoms");

            migrationBuilder.DropTable(
                name: "vector_documents");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "chats");

            migrationBuilder.DropTable(
                name: "rule_specs");

            migrationBuilder.DropTable(
                name: "pdf_documents");

            migrationBuilder.DropTable(
                name: "n8n_configs");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "ai_request_logs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
