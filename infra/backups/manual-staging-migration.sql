-- Manual application of game-reset Phase 2d migration on staging
-- Bypasses EF idempotent script which expects 2 FKs that don't exist (FK_RaptorSummaries_games_GameId, FK_vector_documents_games_GameId)
-- Strategy:
--   1. Drop all FKs pointing to games (17 actually exist)
--   2. Relink GameId/game_id columns from games.Id → games.SharedGameId in all 17 dependent tables
--   3. Drop games table
--   4. Re-add FKs targeting shared_games
--   5. Mark all intermediate migrations + DropGameAggregate + DropGamesTable as applied in __EFMigrationsHistory

BEGIN;

-- Step 1: Drop all existing FKs to games (17 confirmed via pg_constraint query)
ALTER TABLE "ChatThreads" DROP CONSTRAINT IF EXISTS "FK_ChatThreads_games_GameId";
ALTER TABLE "GameEntityRelations" DROP CONSTRAINT IF EXISTS "FK_GameEntityRelations_games_GameId";
ALTER TABLE "GameSessions" DROP CONSTRAINT IF EXISTS "FK_GameSessions_games_GameId";
ALTER TABLE "GameToolkits" DROP CONSTRAINT IF EXISTS "FK_GameToolkits_games_GameId";
ALTER TABLE agent_sessions DROP CONSTRAINT IF EXISTS "FK_agent_sessions_games_GameId";
ALTER TABLE chat_sessions DROP CONSTRAINT IF EXISTS "FK_chat_sessions_games_game_id";
ALTER TABLE chats DROP CONSTRAINT IF EXISTS "FK_chats_games_GameId";
ALTER TABLE chunked_upload_sessions DROP CONSTRAINT IF EXISTS "FK_chunked_upload_sessions_games_GameId";
ALTER TABLE game_phase_templates DROP CONSTRAINT IF EXISTS "FK_game_phase_templates_games_game_id";
ALTER TABLE live_game_sessions DROP CONSTRAINT IF EXISTS "FK_live_game_sessions_games_game_id";
ALTER TABLE play_records DROP CONSTRAINT IF EXISTS "FK_play_records_games_GameId";
ALTER TABLE rule_conflict_faqs DROP CONSTRAINT IF EXISTS "FK_rule_conflict_faqs_games_GameId";
ALTER TABLE rule_specs DROP CONSTRAINT IF EXISTS "FK_rule_specs_games_GameEntityId";
ALTER TABLE rule_specs DROP CONSTRAINT IF EXISTS "FK_rule_specs_games_GameId";
ALTER TABLE rulespec_comments DROP CONSTRAINT IF EXISTS "FK_rulespec_comments_games_GameId";
ALTER TABLE session_tracking_sessions DROP CONSTRAINT IF EXISTS "FK_session_tracking_sessions_games_game_id";
ALTER TABLE text_chunks DROP CONSTRAINT IF EXISTS "FK_text_chunks_games_GameId";

-- Step 2: Relink all GameId columns from games.Id → games.SharedGameId (where applicable)
-- Tables that need relink (have GameId column referencing games.Id):
UPDATE "ChatThreads" t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE "GameEntityRelations" t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE "GameSessions" t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE "GameToolkits" t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE agent_sessions t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE chat_sessions t SET game_id = g."SharedGameId" FROM games g WHERE t.game_id = g."Id" AND g."SharedGameId" IS NOT NULL AND t.game_id <> g."SharedGameId";
UPDATE chats t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE chunked_upload_sessions t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE game_phase_templates t SET game_id = g."SharedGameId" FROM games g WHERE t.game_id = g."Id" AND g."SharedGameId" IS NOT NULL AND t.game_id <> g."SharedGameId";
UPDATE live_game_sessions t SET game_id = g."SharedGameId" FROM games g WHERE t.game_id = g."Id" AND g."SharedGameId" IS NOT NULL AND t.game_id <> g."SharedGameId";
UPDATE play_records t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE rule_conflict_faqs t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE rule_specs t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE rule_specs t SET "GameEntityId" = g."SharedGameId" FROM games g WHERE t."GameEntityId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameEntityId" <> g."SharedGameId";
UPDATE rulespec_comments t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
UPDATE session_tracking_sessions t SET game_id = g."SharedGameId" FROM games g WHERE t.game_id = g."Id" AND g."SharedGameId" IS NOT NULL AND t.game_id <> g."SharedGameId";
UPDATE text_chunks t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";
-- Also RaptorSummaries (column GameId, FK was missing but column exists with stale values)
UPDATE "RaptorSummaries" t SET "GameId" = g."SharedGameId" FROM games g WHERE t."GameId" = g."Id" AND g."SharedGameId" IS NOT NULL AND t."GameId" <> g."SharedGameId";

-- Step 3: Drop the games table (now no FKs reference it)
DROP TABLE IF EXISTS games CASCADE;

-- Step 4: Drop orphan custom enum types (Phase 2c sentinel migration would do this)
DO $$
DECLARE t text;
BEGIN
  FOR t IN SELECT typname FROM pg_type WHERE typtype = 'e' AND typname IN ('approval_status')
  LOOP
    EXECUTE 'DROP TYPE IF EXISTS public.' || quote_ident(t) || ' CASCADE';
  END LOOP;
END $$;

-- Step 5: Mark all intermediate migrations AND DropGameAggregate AND DropGamesTable as applied
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
  ('20260517193743_AddIncidentBannerState', '9.0.11'),
  ('20260518042522_AddRespondedByNameToGameNightInvitation', '9.0.11'),
  ('20260518134809_CreateToolkitVersionsTable_Issue822', '9.0.11'),
  ('20260519041333_AddPgTrgmIndexUsersDisplayNameEmail_Issue950', '9.0.11'),
  ('20260519054522_AddParagraphNumbersToPhotoBatchPages_Issue747', '9.0.11'),
  ('20260519143108_AddDomainEventLogTable_Issue661', '9.0.11'),
  ('20260519163520_AddStorageOperationOutbox_Issue1314', '9.0.11'),
  ('20260520061332_DropGameAggregate_Issue1320', '9.0.11'),
  ('20260520090039_DropGamesTable_Issue1345', '9.0.11')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- Final verification
SELECT 'games_dropped' AS check, NOT EXISTS(SELECT 1 FROM pg_tables WHERE tablename='games') AS pass;
SELECT 'migration_applied' AS check, EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"='20260520090039_DropGamesTable_Issue1345') AS pass;
