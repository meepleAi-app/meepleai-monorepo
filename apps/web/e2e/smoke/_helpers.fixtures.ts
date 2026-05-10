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

// Placeholder UUIDs — sostituire dopo aver eseguito Step 0.4 del plan.
// Format atteso: UUID v4 (es. 'a1b2c3d4-e5f6-4789-90ab-cdef12345678')
export const SEED_GAME_ID = '__REPLACE_ME_FROM_STEP_0_4__';
export const SEED_DOCUMENT_ID = '__REPLACE_ME_FROM_STEP_0_4__';
