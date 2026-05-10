/**
 * Smoke test fixtures — IDs noti dello snapshot DB.
 *
 * ⚠️ PLACEHOLDER VALUES: gli UUID reali dello snapshot DB devono essere
 * identificati al primo run CI tramite Step 0.4 del plan. Vedi:
 * docs/superpowers/plans/2026-05-10-e2e-smoke-game-night.md §Pre-flight 0.4
 *
 * Per identificare gli ID reali:
 *   docker exec meepleai-postgres psql -U postgres -d meepleai -c \
 *     "SELECT id FROM games LIMIT 5;"
 *   docker exec meepleai-postgres psql -U postgres -d meepleai -c \
 *     "SELECT id, game_id FROM pdf_documents WHERE processing_status = 'ready' LIMIT 5;"
 *
 * Sostituisci i placeholder + commit con SHA dello snapshot ref nel commit message.
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md
 */

export const SMOKE_USER = {
  email: 'smoke-aaron@meepleai.test',
  password: 'SmokeAaron1!!',
} as const;

// SEED IDs identificati dallo snapshot DB (DB: meepleai_staging, 2026-05-10).
// Estratti via:
//   docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc \
//     "SELECT id, title FROM shared_games WHERE id IN \
//      (SELECT \"SharedGameId\" FROM pdf_documents WHERE processing_state='Ready');"
//
// Game: Catan — è l'UNICO con un PDF processing_state='Ready' nel snapshot corrente.
// Se lo snapshot viene rigenerato e il PDF Ready cambia, sostituire i 2 UUID sotto.
export const SEED_GAME_ID = '3d54901d-fe9d-4d00-a7ae-e9865bb764f7'; // Catan
export const SEED_DOCUMENT_ID = '47725c26-20b9-41e9-ad80-c00bc1a799d0'; // catan_en_rulebook.pdf
