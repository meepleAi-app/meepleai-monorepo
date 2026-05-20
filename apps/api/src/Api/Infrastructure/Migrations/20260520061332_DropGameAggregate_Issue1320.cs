using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Issue #1320 (P2c): Game domain aggregate has been deleted.
    /// The legacy `games` table and its EF FK navigation properties in GameEntity,
    /// ChatEntity, VectorDocumentEntity, etc. are retained here because
    /// `GameEntity` is still referenced by many EF-mapped entities.
    ///
    /// The actual DROP TABLE `games` and FK column cleanup will follow in a
    /// dedicated Phase-3 migration once all EF FK references are excised.
    ///
    /// This migration serves as a sentinel commit that marks the boundary:
    /// the Game domain aggregate is gone from code; the table survives until Phase 3.
    /// </remarks>
    public partial class DropGameAggregate_Issue1320 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enum cleanup: drop any orphaned PostgreSQL enum types no longer used
            // by the EF model after Game aggregate removal.
            migrationBuilder.Sql(@"
DO $$
DECLARE t text;
BEGIN
  FOR t IN SELECT typname FROM pg_type WHERE typtype = 'e' AND typname IN ('approval_status')
  LOOP
    EXECUTE 'DROP TYPE IF EXISTS public.' || quote_ident(t) || ' CASCADE';
  END LOOP;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Enum re-creation on rollback not required — approval_status enum
            // is only used by the games table, which will be dropped in Phase 3.
        }
    }
}
