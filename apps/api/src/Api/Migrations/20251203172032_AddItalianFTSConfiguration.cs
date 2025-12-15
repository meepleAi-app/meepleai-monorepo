using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // File name must match type name - EF Core migration
#pragma warning disable S101 // Rename class to match pascal case - FTS is standard acronym

namespace Api.Migrations
{
    /// <summary>
    /// ADR-016 Phase 3: Adds Italian full-text search configuration for board game rules.
    /// Creates meepleai_italian text search configuration with game-specific synonyms.
    /// </summary>
    internal partial class AddItalianFTSConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Italian FTS configuration based on the Italian dictionary
            // ADR-016 Phase 3: meepleai_italian configuration for game rules
            // Issue #1996: Check for Italian dictionary availability (GitHub Actions compatibility)
            migrationBuilder.Sql(@"
                -- Drop existing configuration if it exists (for idempotency)
                DROP TEXT SEARCH CONFIGURATION IF EXISTS meepleai_italian CASCADE;

                -- Create Italian text search configuration
                -- Issue #1996: Fallback to 'simple' if 'italian' dictionary not available (CI environments)
                DO $$
                DECLARE
                    italian_exists BOOLEAN;
                BEGIN
                    -- Check if Italian dictionary exists in pg_ts_config
                    SELECT EXISTS (
                        SELECT 1 FROM pg_ts_config WHERE cfgname = 'italian'
                    ) INTO italian_exists;

                    IF italian_exists THEN
                        -- Production: Use Italian dictionary for better stemming
                        CREATE TEXT SEARCH CONFIGURATION meepleai_italian (COPY = pg_catalog.italian);
                        RAISE NOTICE 'Created meepleai_italian configuration with Italian dictionary';
                    ELSE
                        -- CI/Dev: Fallback to simple dictionary
                        CREATE TEXT SEARCH CONFIGURATION meepleai_italian (COPY = pg_catalog.simple);
                        RAISE NOTICE 'Created meepleai_italian configuration with simple dictionary (Italian not available)';
                    END IF;
                END $$;

                -- Create game-specific synonym dictionary
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
                -- Issue #1996: Use simple stem as fallback if italian_stem not available
                DO $$
                DECLARE
                    italian_exists BOOLEAN;
                BEGIN
                    SELECT EXISTS (
                        SELECT 1 FROM pg_ts_config WHERE cfgname = 'italian'
                    ) INTO italian_exists;

                    IF italian_exists THEN
                        -- Production: Use Italian stemming
                        ALTER TEXT SEARCH CONFIGURATION meepleai_italian
                            ALTER MAPPING FOR asciiword, asciihword, hword_asciipart, word, hword, hword_part
                            WITH meepleai_synonyms, italian_stem;
                    ELSE
                        -- CI/Dev: Use simple stemming as fallback
                        ALTER TEXT SEARCH CONFIGURATION meepleai_italian
                            ALTER MAPPING FOR asciiword, asciihword, hword_asciipart, word, hword, hword_part
                            WITH meepleai_synonyms, simple;
                    END IF;
                END $$;

                -- Create index on text_chunks for Italian search if not exists
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector_italian
                    ON text_chunks USING GIN (search_vector);

                COMMENT ON TEXT SEARCH CONFIGURATION meepleai_italian IS
                    'ADR-016 Phase 3: Italian FTS configuration for MeepleAI board game rules search (with CI fallback)';
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
