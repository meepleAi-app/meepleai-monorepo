using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitForeignKeyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRateLimitOverrides",
                table: "UserRateLimitOverrides");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShareRequestLimitConfigs",
                table: "ShareRequestLimitConfigs");

            migrationBuilder.RenameTable(
                name: "UserRateLimitOverrides",
                newName: "user_rate_limit_overrides");

            migrationBuilder.RenameTable(
                name: "ShareRequestLimitConfigs",
                newName: "share_request_limit_configs");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "user_rate_limit_overrides",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "user_rate_limit_overrides",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "user_rate_limit_overrides",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "user_rate_limit_overrides",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "MaxRequestsPerMonth",
                table: "user_rate_limit_overrides",
                newName: "max_requests_per_month");

            migrationBuilder.RenameColumn(
                name: "MaxPendingRequests",
                table: "user_rate_limit_overrides",
                newName: "max_pending_requests");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "user_rate_limit_overrides",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "CreatedByAdminId",
                table: "user_rate_limit_overrides",
                newName: "created_by_admin_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "user_rate_limit_overrides",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CooldownAfterRejectionSeconds",
                table: "user_rate_limit_overrides",
                newName: "cooldown_after_rejection_seconds");

            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "share_request_limit_configs",
                newName: "tier");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "share_request_limit_configs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "share_request_limit_configs",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "MaxRequestsPerMonth",
                table: "share_request_limit_configs",
                newName: "max_requests_per_month");

            migrationBuilder.RenameColumn(
                name: "MaxPendingRequests",
                table: "share_request_limit_configs",
                newName: "max_pending_requests");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "share_request_limit_configs",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "share_request_limit_configs",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CooldownAfterRejectionSeconds",
                table: "share_request_limit_configs",
                newName: "cooldown_after_rejection_seconds");

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "user_rate_limit_overrides",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_rate_limit_overrides",
                table: "user_rate_limit_overrides",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_share_request_limit_configs",
                table: "share_request_limit_configs",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_user_rate_limit_overrides_created_by_admin_id",
                table: "user_rate_limit_overrides",
                column: "created_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_rate_limit_overrides_expires_at",
                table: "user_rate_limit_overrides",
                column: "expires_at",
                filter: "expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_rate_limit_overrides_user_id_active",
                table: "user_rate_limit_overrides",
                column: "user_id",
                unique: true,
                filter: "expires_at IS NULL OR expires_at > NOW()");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_limit_configs_is_active",
                table: "share_request_limit_configs",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_limit_configs_tier_unique_active",
                table: "share_request_limit_configs",
                column: "tier",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.AddForeignKey(
                name: "FK_user_rate_limit_overrides_users_created_by_admin_id",
                table: "user_rate_limit_overrides",
                column: "created_by_admin_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_rate_limit_overrides_users_user_id",
                table: "user_rate_limit_overrides",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_rate_limit_overrides_users_created_by_admin_id",
                table: "user_rate_limit_overrides");

            migrationBuilder.DropForeignKey(
                name: "FK_user_rate_limit_overrides_users_user_id",
                table: "user_rate_limit_overrides");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_rate_limit_overrides",
                table: "user_rate_limit_overrides");

            migrationBuilder.DropIndex(
                name: "ix_user_rate_limit_overrides_created_by_admin_id",
                table: "user_rate_limit_overrides");

            migrationBuilder.DropIndex(
                name: "ix_user_rate_limit_overrides_expires_at",
                table: "user_rate_limit_overrides");

            migrationBuilder.DropIndex(
                name: "ix_user_rate_limit_overrides_user_id_active",
                table: "user_rate_limit_overrides");

            migrationBuilder.DropPrimaryKey(
                name: "PK_share_request_limit_configs",
                table: "share_request_limit_configs");

            migrationBuilder.DropIndex(
                name: "ix_share_request_limit_configs_is_active",
                table: "share_request_limit_configs");

            migrationBuilder.DropIndex(
                name: "ix_share_request_limit_configs_tier_unique_active",
                table: "share_request_limit_configs");

            migrationBuilder.RenameTable(
                name: "user_rate_limit_overrides",
                newName: "UserRateLimitOverrides");

            migrationBuilder.RenameTable(
                name: "share_request_limit_configs",
                newName: "ShareRequestLimitConfigs");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "UserRateLimitOverrides",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "UserRateLimitOverrides",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "UserRateLimitOverrides",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "UserRateLimitOverrides",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "max_requests_per_month",
                table: "UserRateLimitOverrides",
                newName: "MaxRequestsPerMonth");

            migrationBuilder.RenameColumn(
                name: "max_pending_requests",
                table: "UserRateLimitOverrides",
                newName: "MaxPendingRequests");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "UserRateLimitOverrides",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "created_by_admin_id",
                table: "UserRateLimitOverrides",
                newName: "CreatedByAdminId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "UserRateLimitOverrides",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "cooldown_after_rejection_seconds",
                table: "UserRateLimitOverrides",
                newName: "CooldownAfterRejectionSeconds");

            migrationBuilder.RenameColumn(
                name: "tier",
                table: "ShareRequestLimitConfigs",
                newName: "Tier");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ShareRequestLimitConfigs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "ShareRequestLimitConfigs",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "max_requests_per_month",
                table: "ShareRequestLimitConfigs",
                newName: "MaxRequestsPerMonth");

            migrationBuilder.RenameColumn(
                name: "max_pending_requests",
                table: "ShareRequestLimitConfigs",
                newName: "MaxPendingRequests");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "ShareRequestLimitConfigs",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "ShareRequestLimitConfigs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "cooldown_after_rejection_seconds",
                table: "ShareRequestLimitConfigs",
                newName: "CooldownAfterRejectionSeconds");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "UserRateLimitOverrides",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRateLimitOverrides",
                table: "UserRateLimitOverrides",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShareRequestLimitConfigs",
                table: "ShareRequestLimitConfigs",
                column: "Id");
        }
    }
}
