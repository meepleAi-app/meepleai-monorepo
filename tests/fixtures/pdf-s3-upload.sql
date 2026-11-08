-- PDF S3-upload E2E fixture (issue #3846) — applicata DOPO le migration EF Core.
--
-- Scopo: un SharedGame a cui il gate `.github/workflows/e2e-pdf-s3-upload.yml` possa caricare un
-- PDF con STORAGE_PROVIDER=s3, per provare che l'elaborazione rilegge davvero l'oggetto dal bucket.
--
-- ⚠️  Il gioco NON serve solo come destinazione: il suo id è anche il `resourceKey` sotto cui
--   UploadPdfCommandHandler scrive l'oggetto (`resourceKey = gameId ?? privateGameId`). È
--   esattamente la metà della chiave che il lato di lettura ricostruiva dal pdfId — il difetto di
--   #3846. Un gameId diverso dal pdfId è quindi condizione necessaria perché il gate misuri
--   qualcosa: con i due coincidenti il difetto sarebbe invisibile, come lo è sul corpus seeded.
--
-- ⚠️  Applicare DOPO tests/fixtures/smoke-test-users.sql (smoke-user esiste già come INITIAL_ADMIN).
-- Idempotente: ON CONFLICT DO NOTHING.

-- UUID deterministico (v4, non in alcun catalogo reale). Segmento finale "3846" = numero issue.
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
    '00000000-0000-4000-8000-000000003846'::uuid,
    NULL,
    'S3 Upload Gate',
    0,
    'Deterministic PDF-S3-upload fixture — DO NOT delete. See tests/fixtures/pdf-s3-upload.sql',
    2,
    4,
    30,
    8,
    '',
    '',
    2,  -- GameStatus: 2 = Published
    5,  -- GameDataStatus: 5 = Complete
    false,
    u."Id",
    NOW(),
    false,
    false,
    false
FROM users u
WHERE u."Email" = 'smoke-user@meepleai.test'
ON CONFLICT (id) DO NOTHING;
