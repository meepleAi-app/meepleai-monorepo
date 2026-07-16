/**
 * Common / Shared Primitive Schemas
 *
 * Cross-cutting Zod primitives reused by multiple domain schema files.
 */

import { z } from 'zod';

/**
 * Schema for a GAME identifier (board game / shared game / private game id, or a
 * gameId/sharedGameId foreign key pointing at one).
 *
 * Game ids are NOT guaranteed to be RFC-4122 UUIDs: deterministic seed data and
 * legacy rows can produce ids whose version nibble is outside the 1-5 range
 * (e.g. `81cc97e4-f148-fb7b-db36-bf700d1f4561`, version nibble 'f'). Zod's
 * `z.string().uuid()` enforces strict RFC-4122 and rejects these, which broke
 * validation of real backend data on the /discover page (root cause of the
 * bug fixed alongside this schema). Use `GameIdString` instead of
 * `z.string().uuid()` for any id/gameId field belonging to a GAME entity so
 * validation doesn't reject real data. Non-game entity ids (session, user,
 * agent, document, etc.) are always real RFC UUIDs (`Guid.NewGuid()`) and
 * should keep using `z.string().uuid()`.
 *
 * Ref: Issue #2247 (games.schemas.ts precedent), /discover validation root cause.
 */
export const GameIdString = z.string().min(1);
