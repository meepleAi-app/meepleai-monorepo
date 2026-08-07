-- Cover R2-strict E2E fixture (issue #3498) — applicata DOPO le migration EF Core.
--
-- Scopo: un SharedGame nella libreria di smoke-user la cui cover è servita da MinIO, così lo spec
-- apps/web/e2e/smoke-real-backend/cover-r2-strict.spec.ts può provare l'invariante «la cover arriva
-- da R2/MinIO, mai dall'host di input» con un assert di CARICAMENTO REALE (naturalWidth > 0).
--
-- Sorgente cover: WIKIDATA, non PDF. La precedenza del resolver è Pdf → Bgg → Wikidata, ma usare
-- il layer Wikidata evita di dover creare anche un pdf_documents + shared_game_documents: qui
-- interessa che la cover sia risolta da MinIO, non da quale layer.
--
-- ⚠️  CHIAVE / SUFFISSO — il punto in cui è più facile sbagliare:
--   wikidata_cover_r2_key è la chiave **senza suffisso** (`covers/{gameId}/cover`); il resolver
--   chiama GetPresignedUrlForRawKeyAsync(PhysicalKeyFor(Wikidata, dbKey)), che aggiunge `.webp`
--   (CoverKeyBuilder.SuffixFor). L'oggetto in MinIO DEVE quindi stare a:
--       covers/00000000-0000-4000-8000-0000000c0e21/cover.webp
--   Se fosse `-preview.webp` (suffisso del layer Pdf) il presign non troverebbe nulla.
--
-- ⚠️  S3BlobStorageService.GetPresignedUrlForRawKeyAsync fa una HEAD PRIMA di firmare e ritorna
--   null se l'oggetto manca → il DTO cadrebbe sul placeholder e il test fallirebbe con un messaggio
--   fuorviante ("nessuna img"), non con un 404. L'oggetto deve esistere davvero.
--
-- ⚠️  Applicare DOPO tests/fixtures/smoke-test-users.sql (smoke-user esiste già come INITIAL_ADMIN).
-- Idempotente: ON CONFLICT DO NOTHING su entrambe le insert.

-- UUID deterministico (v4, non in alcun catalogo reale). Segmento finale "c0e21" = "cover 21".
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
    has_knowledge_base,
    wikidata_cover_r2_key
)
SELECT
    '00000000-0000-4000-8000-0000000c0e21'::uuid,
    NULL,
    'Catan R2 Strict',
    0,
    'Deterministic cover-R2-strict fixture — DO NOT delete. See tests/fixtures/cover-r2-strict.sql',
    2,
    4,
    30,
    8,
    '',  -- image_url VUOTO di proposito: se la cover si vedesse comunque, arriverebbe da qui
    '',  -- (l'host di input) e non da R2 — cioè esattamente ciò che il test deve escludere.
    2,  -- GameStatus: 2 = Published
    5,  -- GameDataStatus: 5 = Complete
    false,
    u."Id",
    NOW(),
    false,
    false,
    false,
    'covers/00000000-0000-4000-8000-0000000c0e21/cover'
FROM users u
WHERE u."Email" = 'smoke-user@meepleai.test'
ON CONFLICT (id) DO NOTHING;

-- UserLibraryEntry: smoke-user possiede il gioco, così /library lo mostra.
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
    '00000000-0000-4000-8000-0000000c0e22'::uuid,
    u."Id",
    '00000000-0000-4000-8000-0000000c0e21'::uuid,
    NOW(),
    NOW(),
    0,
    0,
    0,
    false
FROM users u
WHERE u."Email" = 'smoke-user@meepleai.test'
ON CONFLICT ("Id") DO NOTHING;
