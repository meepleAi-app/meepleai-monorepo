using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #5150: the name of a live player goes down from 100 to 50 characters, the limit of the correlated game
    /// session the start fills (<c>PlayerNameLimit</c>). The mapper reads a live back through the constructor of
    /// <c>LiveSessionPlayer</c>, which checks the length, so a seat saved with 51 to 100 characters would stop
    /// being readable: it is cut to the limit here. Data only — the column stays <c>varchar(100)</c>, because
    /// narrowing it is an <c>ALTER COLUMN … TYPE</c>, which is not safe to roll back.
    /// </summary>
    /// <remarks>
    /// The domain does not check the uniqueness of the names when it rebuilds a live, so a cut that makes two
    /// names equal does not stop the live from being read. There is no undo: the cut part is gone.
    /// </remarks>
    public partial class TruncateLivePlayerNamesToTheNameLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE session_players
                SET display_name = rtrim(left(display_name, 50))
                WHERE char_length(display_name) > 50;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the old code takes names of up to 100 characters, so it reads the cut ones.
        }
    }
}
