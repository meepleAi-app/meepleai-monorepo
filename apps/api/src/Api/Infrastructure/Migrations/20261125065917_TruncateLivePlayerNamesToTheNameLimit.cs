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
    /// <para>
    /// The length is the domain's, <c>string.Length</c>: UTF-16 code units, where a character above U+FFFF (an
    /// emoji) counts two. Postgres counts characters, so <c>char_length</c> and <c>left</c> would leave over
    /// the limit a name with emoji, and that live unreadable. The cut walks the characters and stops before
    /// the one that would cross the limit, so no character is split.
    /// </para>
    /// <para>
    /// The domain does not check the uniqueness of the names when it rebuilds a live, so a cut that makes two
    /// names equal does not stop the live from being read. There is no undo: the cut part is gone.
    /// </para>
    /// </remarks>
    public partial class TruncateLivePlayerNamesToTheNameLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One statement, plain SQL (the idempotent script wraps every statement in its own DO block). Each
            // character weighs its UTF-16 units; the running sum keeps the characters that fit, in order. A name
            // over 50 units has more than 25 characters, so the filter only narrows the scan.
            migrationBuilder.Sql(
                """
                UPDATE session_players AS seat
                SET display_name = rtrim(kept.name)
                FROM (
                    SELECT ch.id, string_agg(ch.c, '' ORDER BY ch.n) AS name
                    FROM (
                        SELECT p.id, t.c, t.n,
                               sum(CASE WHEN ascii(t.c) > 65535 THEN 2 ELSE 1 END)
                                   OVER (PARTITION BY p.id ORDER BY t.n) AS units
                        FROM session_players AS p
                        CROSS JOIN LATERAL regexp_split_to_table(p.display_name, '') WITH ORDINALITY AS t(c, n)
                        WHERE char_length(p.display_name) > 25
                    ) AS ch
                    WHERE ch.units <= 50
                    GROUP BY ch.id
                ) AS kept
                WHERE seat.id = kept.id
                  AND kept.name <> seat.display_name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the old code takes names of up to 100 characters, so it reads the cut ones.
        }
    }
}
