-- Smoke test SharedGame + UserLibraryEntry fixture — applied AFTER EF Core migrations.
--
-- Purpose: provide a deterministic SharedGame ID + library entry for smoke-user
-- (admin) so that nightly E2E specs in apps/web/e2e/smoke-real-backend/game-night-*.smoke.spec.ts
-- can navigate to /library/games/{id}?tab=aiChat without runtime seeding (which
-- requires admin SharedGame creation endpoint we don't have).
--
-- Used by:
--   apps/web/e2e/smoke-real-backend/game-night-low-confidence.smoke.spec.ts
--   apps/web/e2e/smoke-real-backend/game-night-out-of-context.smoke.spec.ts
--   apps/web/e2e/smoke-real-backend/game-night-pdf-non-owner.smoke.spec.ts
--
-- All chat UI behavior is mocked at SSE level (mockQaStreamV2) — SharedGame doesn't
-- need a real KB / PDF / vector index. Only the route /library/games/{id} must resolve.
--
-- ⚠️  Apply AFTER tests/fixtures/smoke-test-users.sql (smoke-user already exists as INITIAL_ADMIN).
-- Idempotent: ON CONFLICT DO NOTHING for both inserts.

-- Deterministic SharedGame UUID (v4, not in any real catalog)
-- Last segment "53e1d" encodes "seedi" = "seeded" shorthand
INSERT INTO shared_games (
    id,
    bgg_id,
    title,
    year_published,
    description,
    min_players,
    max_players,
    playing_time_minutes,
    min_age,
    image_url,
    thumbnail_url,
    status,
    "GameDataStatus",
    "HasUploadedPdf",
    created_by,
    created_at,
    is_deleted,
    is_rag_public,
    has_knowledge_base
)
SELECT
    '00000000-0000-4000-8000-000000053e1d'::uuid,
    NULL,
    'Smoke Chat Fixture Game',
    0,
    'Deterministic smoke test fixture — DO NOT delete. See tests/fixtures/smoke-test-games.sql',
    2,
    4,
    30,
    8,
    '',
    '',
    1,  -- 1 = Published
    5,  -- 5 = Complete
    false,
    u."Id",
    NOW(),
    false,
    false,
    false
FROM users u
WHERE u."Email" = 'smoke-user@meepleai.test'
ON CONFLICT (id) DO NOTHING;

-- UserLibraryEntry: smoke-user owns the smoke fixture game
INSERT INTO user_library_entries (
    "Id",
    "UserId",
    shared_game_id,
    "AddedAt",
    "OwnershipDeclaredAt",
    "CurrentState",
    "TimesPlayed",
    "CompetitiveSessions",
    "IsFavorite"
)
SELECT
    '00000000-0000-4000-8000-000000053e1e'::uuid,
    u."Id",
    '00000000-0000-4000-8000-000000053e1d'::uuid,
    NOW(),
    NOW(),
    0,
    0,
    0,
    false
FROM users u
WHERE u."Email" = 'smoke-user@meepleai.test'
ON CONFLICT ("Id") DO NOTHING;
