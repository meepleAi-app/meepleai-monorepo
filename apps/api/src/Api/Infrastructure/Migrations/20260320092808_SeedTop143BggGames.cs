using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedTop143BggGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games",
                sql: "(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games",
                sql: "playing_time_minutes >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games",
                sql: "year_published = 0 OR (year_published > 1900 AND year_published <= 2100)");

            // Step 2: Create system seed admin user (idempotent)
            migrationBuilder.Sql("""
                INSERT INTO users (
                    "Id", "Email", "DisplayName", "PasswordHash", "Role", "Tier",
                    "CreatedAt", "IsDemoAccount", "Language", "EmailNotifications",
                    "Theme", "DataRetentionDays", "IsTwoFactorEnabled", "EmailVerified",
                    "IsSuspended", "Status", "Level", "ExperiencePoints",
                    "FailedLoginAttempts", "IsContributor", "OnboardingCompleted", "OnboardingSkipped"
                )
                VALUES (
                    gen_random_uuid(), 'system-seed@meepleai.app', 'System Seed Admin', NULL, 'admin', 'free',
                    NOW(), false, 'en', false,
                    'system', 90, false, true,
                    false, 'Active', 1, 0,
                    0, false, true, false
                )
                ON CONFLICT ("Email") DO NOTHING;
                """);

            // Step 3: Seed 143 top BGG games as skeleton entries using CTE + CROSS JOIN
            migrationBuilder.Sql("""
                WITH seed_admin AS (
                    SELECT "Id" FROM users WHERE "Email" = 'system-seed@meepleai.app'
                ),
                game_data(bgg_id, title) AS (VALUES
                    (178900, 'Codenames'), (266192, 'Wingspan'), (167791, 'Terraforming Mars'),
                    (68448, '7 Wonders'), (9209, 'Ticket to Ride'), (148228, 'Splendor'),
                    (14996, 'Ticket to Ride: Europe'), (36218, 'Dominion'), (169786, 'Scythe'),
                    (163412, 'Patchwork'), (129622, 'Love Letter'), (70323, 'King of Tokyo'),
                    (39856, 'Dixit'), (174430, 'Gloomhaven'), (40692, 'Small World'),
                    (199792, 'Everdell'), (1927, 'Munchkin'), (65244, 'Forbidden Island'),
                    (237182, 'Root'), (133473, 'Sushi Go!'), (162886, 'Spirit Island'),
                    (54043, 'Jaipur'), (244521, 'The Quacks of Quedlinburg'),
                    (284083, 'The Crew: Quest for Planet Nine'), (131357, 'Coup'),
                    (172225, 'Exploding Kittens'), (31260, 'Agricola'), (204583, 'Kingdomino'),
                    (98778, 'Hanabi'), (161936, 'Pandemic Legacy: Season 1'), (295947, 'Cascadia'),
                    (291457, 'Gloomhaven: Jaws of the Lion'), (2651, 'Power Grid'),
                    (10547, 'Betrayal at House on the Hill'), (342942, 'Ark Nova'),
                    (3076, 'Puerto Rico'), (312484, 'Lost Ruins of Arnak'),
                    (84876, 'The Castles of Burgundy'), (205637, 'Arkham Horror: The Card Game'),
                    (181304, 'Mysterium'), (66690, 'Dominion: Prosperity'),
                    (209685, 'Century: Spice Road'), (12692, 'Gloom'), (154597, 'Hive Pocket'),
                    (170042, 'Raiders of the North Sea'), (221107, 'Pandemic Legacy: Season 2'),
                    (154203, 'Imperial Settlers'), (116, 'Guillotine'), (414317, 'Harmonies'),
                    (92828, 'Dixit: Odyssey'), (158600, 'Hanamikoji'), (255984, 'Sleeping Gods'),
                    (2452, 'Jenga'), (93, 'El Grande'), (191189, 'Aeon''s End'),
                    (262712, 'Res Arcana'), (198994, 'Hero Realms'), (169426, 'Roll Player'),
                    (2243, 'Yahtzee'), (2223, 'UNO'), (822, 'Carcassonne'), (181, 'Risk'),
                    (171, 'Chess'), (2083, 'Checkers'), (30549, 'Pandemic'), (1406, 'Monopoly'),
                    (320, 'Scrabble'), (2394, 'Dominoes'), (188, 'Go'), (2397, 'Backgammon'),
                    (1294, 'Clue'), (74, 'Apples to Apples'), (163, 'Balderdash'),
                    (1293, 'Boggle'), (2381, 'Scattergories'), (27225, 'Bananagrams'),
                    (2653, 'Survive: Escape from Atlantis!'), (63268, 'Spot it!'),
                    (2719, 'Connect Four'), (4143, 'Guess Who?'), (5432, 'Chutes and Ladders'),
                    (2407, 'Sorry'), (224517, 'Brass: Birmingham'), (316554, 'Dune: Imperium'),
                    (233078, 'Twilight Imperium: Fourth Edition'),
                    (397598, 'Dune: Imperium - Uprising'),
                    (115746, 'War of the Ring: Second Edition'), (187645, 'Star Wars: Rebellion'),
                    (220308, 'Gaia Project'), (12333, 'Twilight Struggle'),
                    (182028, 'Through the Ages: A New Story of Civilization'),
                    (193738, 'Great Western Trail'),
                    (418059, 'SETI: Search for Extraterrestrial Intelligence'),
                    (295770, 'Frosthaven'), (246900, 'Eclipse: Second Dawn for the Galaxy'),
                    (338960, 'Slay the Spire: The Board Game'), (28720, 'Brass: Lancashire'),
                    (173346, '7 Wonders Duel'), (167355, 'Nemesis'), (177736, 'A Feast for Odin'),
                    (421006, 'The Lord of the Rings: The Card Game'),
                    (266507, 'Clank! Legacy: Acquisitions Incorporated'), (124361, 'Concordia'),
                    (341169, 'Great Western Trail: New Zealand'),
                    (373106, 'Skytear: Arena of Legends'), (120677, 'Terra Mystica'),
                    (192135, 'Too Many Bones'), (164928, 'Orleans'),
                    (96848, 'Mage Knight Board Game'), (251247, 'Barrage'),
                    (321608, 'Hegemony: Lead Your Class to Victory'),
                    (183394, 'Viticulture Essential Edition'), (521, 'Crokinole'),
                    (366013, 'Heat: Pedal to the Metal'), (284378, 'Kanban EV'),
                    (285774, 'Marvel Champions: The Card Game'), (247763, 'Underwater Cities'),
                    (175914, 'Food Chain Magnate'), (256960, 'Pax Pamir: Second Edition'),
                    (253344, 'Cthulhu: Death May Die'), (383179, 'Age of Innovation'),
                    (201808, 'Clank!: A Deck-Building Adventure'), (371942, 'The White Castle'),
                    (266810, 'Paladins of the West Kingdom'), (35677, 'Le Havre'),
                    (125153, 'The Gallerist'), (124742, 'Android: Netrunner'),
                    (164153, 'Star Wars: Imperial Assault'),
                    (380607, 'Great Western Trail: Argentina'),
                    (200680, 'Agricola (Revised Edition)'), (276025, 'Maracaibo'),
                    (209010, 'Mechs vs. Minions'), (55690, 'Kingdom Death: Monster'),
                    (322289, 'Darwin''s Journey'), (332772, 'Revive'),
                    (28143, 'Race for the Galaxy'), (338111, 'Voidfall'),
                    (366161, 'Wingspan: Asia'), (230802, 'Azul'), (157354, 'Five Tribes'),
                    (277659, 'Final Girl'), (159675, 'Fields of Arle'),
                    (72125, 'Eclipse: New Dawn for the Galaxy')
                )
                INSERT INTO shared_games (
                    id, bgg_id, title, year_published, description,
                    min_players, max_players, playing_time_minutes, min_age,
                    complexity_rating, average_rating, image_url, thumbnail_url,
                    status, "GameDataStatus", "HasUploadedPdf", is_deleted,
                    is_rag_public, created_by, created_at, modified_at
                )
                SELECT
                    gen_random_uuid(), gd.bgg_id, gd.title, 0, '',
                    0, 0, 0, 0,
                    NULL, NULL, '', '',
                    0, 0, false, false,
                    false, sa."Id", NOW(), NOW()
                FROM game_data gd CROSS JOIN seed_admin sa
                ON CONFLICT (bgg_id) WHERE bgg_id IS NOT NULL DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all seeded games (regardless of enrichment status) then the seed admin user
            migrationBuilder.Sql("""
                DELETE FROM shared_games
                WHERE created_by = (SELECT "Id" FROM users WHERE "Email" = 'system-seed@meepleai.app');

                DELETE FROM users WHERE "Email" = 'system-seed@meepleai.app';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games",
                sql: "min_players > 0 AND max_players >= min_players");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games",
                sql: "playing_time_minutes > 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games",
                sql: "year_published > 1900 AND year_published <= 2100");
        }
    }
}
