using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlackNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "game_toolbox");

            migrationBuilder.AddColumn<bool>(
                name: "SlackEnabled",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnBadgeEarned",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnDocumentFailed",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnDocumentReady",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnGameNightInvitation",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnGameNightReminder",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnRetryAvailable",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnShareRequestApproved",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlackOnShareRequestCreated",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_requests_invitation_tokens_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitation_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_access_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification_queue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    slack_channel_target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    slack_team_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_queue_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "slack_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slack_user_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slack_team_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slack_team_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bot_access_token = table.Column<string>(type: "text", nullable: false),
                    dm_channel_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disconnected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "slack_team_channel_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    webhook_url = table.Column<string>(type: "text", nullable: false),
                    notification_types = table.Column<string>(type: "jsonb", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    overrides_default = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_team_channel_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_templates",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tools_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    phases_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    shared_context_defaults_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolboxes",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    shared_context = table.Column<string>(type: "jsonb", nullable: false),
                    current_phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolboxes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_phases",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active_tool_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolbox_phases_toolboxes_toolbox_id",
                        column: x => x.toolbox_id,
                        principalSchema: "game_toolbox",
                        principalTable: "toolboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_tools",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    state = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_tools", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolbox_tools_toolboxes_toolbox_id",
                        column: x => x.toolbox_id,
                        principalSchema: "game_toolbox",
                        principalTable: "toolboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_email_status",
                table: "access_requests",
                columns: new[] { "email", "status" },
                unique: true,
                filter: "\"status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_invitation_id",
                table: "access_requests",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_requested_at",
                table: "access_requests",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_reviewed_by",
                table: "access_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_channel_type_status",
                table: "notification_queue_items",
                columns: new[] { "channel_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_correlation_id",
                table: "notification_queue_items",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_recipient_user_id_created_at",
                table: "notification_queue_items",
                columns: new[] { "recipient_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_status_next_retry_at",
                table: "notification_queue_items",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_slack_connections_is_active",
                table: "slack_connections",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_slack_connections_slack_user_id",
                table: "slack_connections",
                column: "slack_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_slack_connections_user_id_slack_team_id",
                table: "slack_connections",
                columns: new[] { "user_id", "slack_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slack_team_channel_configs_channel_name",
                table: "slack_team_channel_configs",
                column: "channel_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slack_team_channel_configs_is_enabled",
                table: "slack_team_channel_configs",
                column: "is_enabled",
                filter: "is_enabled = true");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_phases_toolbox_id",
                schema: "game_toolbox",
                table: "toolbox_phases",
                column: "toolbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_templates_game_id",
                schema: "game_toolbox",
                table: "toolbox_templates",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_tools_toolbox_id",
                schema: "game_toolbox",
                table: "toolbox_tools",
                column: "toolbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolboxes_game_id",
                schema: "game_toolbox",
                table: "toolboxes",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolboxes_is_deleted",
                schema: "game_toolbox",
                table: "toolboxes",
                column: "is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_requests");

            migrationBuilder.DropTable(
                name: "notification_queue_items");

            migrationBuilder.DropTable(
                name: "slack_connections");

            migrationBuilder.DropTable(
                name: "slack_team_channel_configs");

            migrationBuilder.DropTable(
                name: "toolbox_phases",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolbox_templates",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolbox_tools",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolboxes",
                schema: "game_toolbox");

            migrationBuilder.DropColumn(
                name: "SlackEnabled",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnBadgeEarned",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnDocumentFailed",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnDocumentReady",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnGameNightInvitation",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnGameNightReminder",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnRetryAvailable",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnShareRequestApproved",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "SlackOnShareRequestCreated",
                table: "notification_preferences");
        }
    }
}
