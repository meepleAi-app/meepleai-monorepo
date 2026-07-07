/**
 * GameBook catalog API client — Phase E1 of the gamebook multi-book
 * generalization (spec
 * `docs/superpowers/specs/2026-05-19-gamebook-multi-book-generalization-design.md`).
 *
 * Endpoints:
 *   - GET /api/v1/gamebook/books?gameRefId=<guid>&gameRefKind=<0|1>
 *
 * Backed by `ListGameBooksByGameQueryHandler` (GameManagement BC).
 *
 * `GameRef` mirrors the backend value object: a discriminated reference to
 * either a SharedGame (kind=0) or a PrivateGame (kind=1). Issue #1320.
 */

import { z } from 'zod';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Discriminator for `GameRef`. Mirrors backend `GameRefKind` enum.
 */
export const GameRefKind = {
  Shared: 0,
  Private: 1,
} as const;
export type GameRefKindValue = (typeof GameRefKind)[keyof typeof GameRefKind];

/**
 * Discriminated reference to either a SharedGame (kind=0) or a PrivateGame (kind=1).
 *
 * The pair `{ id, kind }` is required because the backend resolves the target
 * aggregate by table: SharedGame for kind=0, PrivateGame for kind=1.
 */
export interface GameRef {
  id: string;
  kind: GameRefKindValue;
}

/**
 * GameBook DTO mirroring `Api.BoundedContexts.GameManagement.Application.DTOs.GameBookDto`.
 *
 * `roles` encodes a GameBookRole bitflag: Tutorial=1, RulesReference=2,
 * Narrative=4, Encounter=8, Lore=16, Setup=32. Use bitwise AND to filter by
 * role (see `hasRole` / `rolesToNames`).
 *
 * `paragraphScheme`: 0=None, 1=ParagraphNumber, 2=Section.
 */
export const GameBookDtoSchema = z.object({
  id: z.string().uuid(),
  gameRefId: z.string().uuid(),
  gameRefKind: z.number().int(),
  ownerUserId: z.string().uuid().nullable(),
  displayName: z.string().min(1),
  roles: z.number().int(),
  paragraphScheme: z.number().int(),
  language: z.string(),
  sequentialRead: z.boolean(),
  kbSourceDocId: z.string().uuid().nullable(),
  physicalOnly: z.boolean(),
  createdAt: z.string(),
});

export type GameBookDto = z.infer<typeof GameBookDtoSchema>;

const ListGameBooksResponseSchema = z.array(GameBookDtoSchema);

/**
 * GameBookRole bitflag values — mirrors backend `[Flags] enum GameBookRole`
 * (GameManagement BC) exactly:
 *   None=0, Tutorial=1, RulesReference=2, Narrative=4, Encounter=8, Lore=16, Setup=32
 *
 * Kept in lock-step by `src/lib/api/__tests__/gamebook.test.ts`. Divergence
 * mislabels the `roles` int decoded from the wire (issue #2637 SI-6).
 */
export const GameBookRole = {
  Tutorial: 1,
  RulesReference: 2,
  Narrative: 4,
  Encounter: 8,
  Lore: 16,
  Setup: 32,
} as const;

export type GameBookRoleName = keyof typeof GameBookRole;

/**
 * Canonical display order for the role badges rendered by the book-manager.
 * Reading-flow order: onboarding roles first, then reference/story material.
 */
export const GAME_BOOK_ROLE_ORDER = [
  'Tutorial',
  'Setup',
  'RulesReference',
  'Narrative',
  'Encounter',
  'Lore',
] as const satisfies readonly GameBookRoleName[];

/**
 * Returns true if the supplied bitflag includes the given role.
 */
export function hasRole(roles: number, role: number): boolean {
  return (roles & role) !== 0;
}

/**
 * Decodes a `roles` bitflag into the list of present role names, in canonical
 * display order (`GAME_BOOK_ROLE_ORDER`). Empty array for `None` (0).
 */
export function rolesToNames(roles: number): GameBookRoleName[] {
  return GAME_BOOK_ROLE_ORDER.filter(name => hasRole(roles, GameBookRole[name]));
}

/**
 * List all `GameBook` rows for the given GameRef.
 *
 * For private games the backend scopes the result to the authenticated user.
 * For shared games community books are returned regardless of caller.
 */
export async function listGameBooks(
  gameRef: GameRef,
  signal?: AbortSignal
): Promise<GameBookDto[]> {
  const params = new URLSearchParams({
    gameRefId: gameRef.id,
    gameRefKind: String(gameRef.kind),
  });

  const res = await fetch(`${API_BASE}/api/v1/gamebook/books?${params.toString()}`, {
    credentials: 'include',
    signal,
  });

  if (!res.ok) {
    let detail = '';
    try {
      detail = await res.text();
    } catch {
      /* ignore body read failure */
    }
    throw new Error(`GameBook list API error ${res.status}: ${detail || res.statusText}`);
  }

  const raw = (await res.json()) as unknown;
  return ListGameBooksResponseSchema.parse(raw);
}
