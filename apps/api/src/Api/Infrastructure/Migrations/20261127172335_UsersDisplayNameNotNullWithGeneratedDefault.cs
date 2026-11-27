using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #5160 — <c>users."DisplayName"</c> becomes NOT NULL, with a generated name as its default. First the rows that
    /// would have shown an address, or nothing, get a generated name, the same shape registration and OAuth give
    /// (<c>Player-</c> and seven hex digits):
    /// <list type="bullet">
    ///   <item>no name, or only blanks;</item>
    ///   <item>the account's own address, whatever the case and the spaces — what <c>UserRepository.UpdateAsync</c>
    ///   wrote back before #4780, from the address <c>MapToDomain</c> used in place of a missing name;</item>
    ///   <item>any name with an «@»: an older address saved the same way by a user who then changed email.
    ///   Registration and the profile refuse an «@» in a name, so this is never a name someone chose there; an OAuth
    ///   name with one is replaced too, as decided for <c>DJ@Home.TV</c> (#4780).</item>
    /// </list>
    /// The NOT NULL is set here by SQL, not by the EF model: <c>IsRequired()</c> would make the InMemory provider refuse
    /// every test entity built without a name. <c>Down</c> drops the NOT NULL and the default; it does not put the
    /// addresses back.
    /// </summary>
    public partial class UsersDisplayNameNotNullWithGeneratedDefault : Migration
    {
        /// <summary>The backfill, run before the column becomes NOT NULL. Read by its test, so that it is the one tested.</summary>
        internal const string BackfillSql = """
            UPDATE users
            SET "DisplayName" = 'Player-' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 7)
            WHERE "DisplayName" IS NULL
               OR btrim("DisplayName") = ''
               OR lower(btrim("DisplayName")) = lower(btrim("Email"))
               OR position('@' in "DisplayName") > 0;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BackfillSql);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                defaultValueSql: "'Player-' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 7)",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.Sql("""ALTER TABLE users ALTER COLUMN "DisplayName" SET NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE users ALTER COLUMN "DisplayName" DROP NOT NULL;""");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldDefaultValueSql: "'Player-' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 7)");
        }
    }
}
