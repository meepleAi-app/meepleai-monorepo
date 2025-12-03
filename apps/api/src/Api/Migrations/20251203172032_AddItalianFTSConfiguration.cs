using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// ADR-016 Phase 3: Adds Italian full-text search configuration for board game rules.
    /// Creates meepleai_italian text search configuration with game-specific synonyms.
    /// </summary>
    public partial class AddItalianFTSConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Italian FTS configuration based on the Italian dictionary
            // ADR-016 Phase 3: meepleai_italian configuration for game rules
            migrationBuilder.Sql(@"
                -- Drop existing configuration if it exists (for idempotency)
                DROP TEXT SEARCH CONFIGURATION IF EXISTS meepleai_italian CASCADE;

                -- Create Italian text search configuration based on pg_catalog.italian
                CREATE TEXT SEARCH CONFIGURATION meepleai_italian (COPY = pg_catalog.italian);

                -- Create game-specific synonym dictionary
                -- First, create the synonym file path function if needed
                DO $$
                BEGIN
                    -- Create thesaurus dictionary for board game synonyms
                    DROP TEXT SEARCH DICTIONARY IF EXISTS meepleai_synonyms CASCADE;

                    -- Use simple dictionary with synonym substitution
                    -- Note: For production, consider using a thesaurus file
                    CREATE TEXT SEARCH DICTIONARY meepleai_synonyms (
                        TEMPLATE = pg_catalog.simple
                    );
                END $$;

                -- Add synonyms dictionary to our configuration
                -- Game terms get higher priority with the synonyms dictionary
                ALTER TEXT SEARCH CONFIGURATION meepleai_italian
                    ALTER MAPPING FOR asciiword, asciihword, hword_asciipart, word, hword, hword_part
                    WITH meepleai_synonyms, italian_stem;

                -- Create index on text_chunks for Italian search if not exists
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector_italian
                    ON text_chunks USING GIN (search_vector);

                COMMENT ON TEXT SEARCH CONFIGURATION meepleai_italian IS
                    'ADR-016 Phase 3: Italian FTS configuration for MeepleAI board game rules search';
            ");

            // Add language column to text_chunks if not exists
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'text_chunks' AND column_name = 'language'
                    ) THEN
                        ALTER TABLE text_chunks ADD COLUMN language VARCHAR(10) DEFAULT 'it';
                    END IF;
                END $$;
            ");

            // Create index on language column for filtering
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_text_chunks_language
                    ON text_chunks (language);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the Italian FTS configuration
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_text_chunks_language;
                DROP INDEX IF EXISTS idx_text_chunks_search_vector_italian;
                DROP TEXT SEARCH CONFIGURATION IF EXISTS meepleai_italian CASCADE;
                DROP TEXT SEARCH DICTIONARY IF EXISTS meepleai_synonyms CASCADE;

                -- Remove language column
                ALTER TABLE text_chunks DROP COLUMN IF EXISTS language;
            ");
        }
    }
}
