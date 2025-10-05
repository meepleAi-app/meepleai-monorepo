using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agents_tenants_TenantId",
                table: "agents");

            migrationBuilder.DropForeignKey(
                name: "FK_chat_logs_tenants_TenantId",
                table: "chat_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_chats_tenants_TenantId",
                table: "chats");

            migrationBuilder.DropForeignKey(
                name: "FK_games_tenants_TenantId",
                table: "games");

            migrationBuilder.DropForeignKey(
                name: "FK_pdf_documents_tenants_TenantId",
                table: "pdf_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_tenants_TenantId",
                table: "rule_specs");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_tenants_TenantId",
                table: "user_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_tenants_TenantId",
                table: "vector_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_n8n_configs_tenants_TenantId",
                table: "n8n_configs");

            migrationBuilder.DropIndex(
                name: "IX_agents_TenantId_GameId_Name",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_ai_request_logs_TenantId_CreatedAt",
                table: "ai_request_logs");

            migrationBuilder.DropIndex(
                name: "IX_ai_request_logs_TenantId_Endpoint_CreatedAt",
                table: "ai_request_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_TenantId_CreatedAt",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_chat_logs_TenantId_ChatId_CreatedAt",
                table: "chat_logs");

            migrationBuilder.DropIndex(
                name: "IX_chats_TenantId_GameId_StartedAt",
                table: "chats");

            migrationBuilder.DropIndex(
                name: "IX_games_TenantId_Name",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_n8n_configs_TenantId_Name",
                table: "n8n_configs");

            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_TenantId_GameId_UploadedAt",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "IX_rule_specs_TenantId_GameId_Version",
                table: "rule_specs");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_TenantId_UserId",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_users_TenantId_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_vector_documents_TenantId_GameId",
                table: "vector_documents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "n8n_configs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "chat_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "vector_documents");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_Name",
                table: "agents",
                columns: new[] { "GameId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_CreatedAt",
                table: "ai_request_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_Endpoint_CreatedAt",
                table: "ai_request_logs",
                columns: new[] { "Endpoint", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_ChatId_CreatedAt",
                table: "chat_logs",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chats_GameId_StartedAt",
                table: "chats",
                columns: new[] { "GameId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_games_Name",
                table: "games",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_Name",
                table: "n8n_configs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "GameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameId_Version",
                table: "rule_specs",
                columns: new[] { "GameId", "Version" },
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_GameId_Name",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_ai_request_logs_CreatedAt",
                table: "ai_request_logs");

            migrationBuilder.DropIndex(
                name: "IX_ai_request_logs_Endpoint_CreatedAt",
                table: "ai_request_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_chat_logs_ChatId_CreatedAt",
                table: "chat_logs");

            migrationBuilder.DropIndex(
                name: "IX_chats_GameId_StartedAt",
                table: "chats");

            migrationBuilder.DropIndex(
                name: "IX_games_Name",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_n8n_configs_Name",
                table: "n8n_configs");

            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "IX_rule_specs_GameId_Version",
                table: "rule_specs");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_UserId",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_vector_documents_GameId",
                table: "vector_documents");

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "user_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "rule_specs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "pdf_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "n8n_configs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "games",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "chat_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "chats",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "agents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ai_request_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "audit_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "vector_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_agents_TenantId_GameId_Name",
                table: "agents",
                columns: new[] { "TenantId", "GameId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_TenantId_CreatedAt",
                table: "ai_request_logs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_TenantId_Endpoint_CreatedAt",
                table: "ai_request_logs",
                columns: new[] { "TenantId", "Endpoint", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TenantId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_TenantId_ChatId_CreatedAt",
                table: "chat_logs",
                columns: new[] { "TenantId", "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chats_TenantId_GameId_StartedAt",
                table: "chats",
                columns: new[] { "TenantId", "GameId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_games_TenantId_Name",
                table: "games",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_TenantId_Name",
                table: "n8n_configs",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_TenantId_GameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "TenantId", "GameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_TenantId_GameId_Version",
                table: "rule_specs",
                columns: new[] { "TenantId", "GameId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_TenantId_UserId",
                table: "user_sessions",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_Email",
                table: "users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_TenantId_GameId",
                table: "vector_documents",
                columns: new[] { "TenantId", "GameId" });

            migrationBuilder.AddForeignKey(
                name: "FK_agents_tenants_TenantId",
                table: "agents",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_logs_tenants_TenantId",
                table: "chat_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chats_tenants_TenantId",
                table: "chats",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_games_tenants_TenantId",
                table: "games",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pdf_documents_tenants_TenantId",
                table: "pdf_documents",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_tenants_TenantId",
                table: "rule_specs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_sessions_tenants_TenantId",
                table: "user_sessions",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_tenants_TenantId",
                table: "vector_documents",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_n8n_configs_tenants_TenantId",
                table: "n8n_configs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
