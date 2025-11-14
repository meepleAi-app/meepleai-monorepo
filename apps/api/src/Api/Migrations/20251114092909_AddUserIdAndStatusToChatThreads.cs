using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdAndStatusToChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Status column (always safe, has default value)
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChatThreads",
                type: "text",
                nullable: false,
                defaultValue: "active");

            // Step 1: Add UserId as NULLABLE initially (safe for existing rows)
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ChatThreads",
                type: "uuid",
                nullable: true, // Nullable during migration
                defaultValue: null);

            // Step 2: Backfill existing rows with first admin user
            // This SQL finds the first admin user and assigns all orphaned threads to them
            // If no admin exists, the migration will fail explicitly
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    admin_id UUID;
                    orphaned_count INT;
                BEGIN
                    -- Check if there are any orphaned threads
                    SELECT COUNT(*) INTO orphaned_count
                    FROM ""ChatThreads""
                    WHERE ""UserId"" IS NULL;

                    -- Only proceed if there are orphaned threads
                    IF orphaned_count > 0 THEN
                        -- Find first admin user
                        SELECT ""Id"" INTO admin_id
                        FROM ""users""
                        WHERE ""Role"" = 'Admin'
                        ORDER BY ""CreatedAt""
                        LIMIT 1;

                        -- Fail migration if no admin exists
                        IF admin_id IS NULL THEN
                            RAISE EXCEPTION 'Migration failed: No admin user found to assign orphaned chat threads. Please create an admin user first or delete orphaned threads.';
                        END IF;

                        -- Backfill with admin user
                        UPDATE ""ChatThreads""
                        SET ""UserId"" = admin_id
                        WHERE ""UserId"" IS NULL;
                        
                        RAISE NOTICE 'Successfully backfilled % orphaned threads to admin user %', orphaned_count, admin_id;
                    ELSE
                        RAISE NOTICE 'No orphaned threads found, skipping backfill';
                    END IF;
                END $$;
            ");

            // Step 3: Make UserId NOT NULL after backfill
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ChatThreads",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Step 4: Add index and FK constraint (safe now that all rows have valid UserId)
            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatThreads_users_UserId",
                table: "ChatThreads",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatThreads_users_UserId",
                table: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatThreads");
        }
    }
}