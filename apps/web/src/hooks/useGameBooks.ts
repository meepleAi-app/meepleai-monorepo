/**
 * `useGameBooks` — TanStack Query hook for listing GameBook rows by GameRef.
 *
 * Phase E1 of the gamebook multi-book generalization (spec
 * `docs/superpowers/specs/2026-05-19-gamebook-multi-book-generalization-design.md`).
 *
 * Calls `GET /api/v1/gamebook/books?gameRefId=&gameRefKind=` via `listGameBooks`.
 * The query stays disabled until a non-null `gameRef` is provided, so callers
 * can lift the hook above the conditional without invoking a noisy `null` fetch.
 *
 * @example
 * const { data: books, isLoading } = useGameBooks(gameRef);
 * const narrative = books?.filter(b => hasRole(b.roles, GameBookRole.Narrative)) ?? [];
 */

import { useQuery } from '@tanstack/react-query';

import { listGameBooks, type GameBookDto, type GameRef } from '@/lib/api/gamebook';

export const gameBookKeys = {
  all: ['gameBooks'] as const,
  list: (gameRef: GameRef | null) =>
    gameRef === null
      ? ([...gameBookKeys.all, 'disabled'] as const)
      : ([...gameBookKeys.all, gameRef.kind, gameRef.id] as const),
};

export function useGameBooks(gameRef: GameRef | null) {
  return useQuery<GameBookDto[]>({
    queryKey: gameBookKeys.list(gameRef),
    queryFn: ({ signal }) => {
      if (gameRef === null) {
        // `enabled: false` prevents this branch in practice; guard satisfies TS.
        return Promise.resolve([]);
      }
      return listGameBooks(gameRef, signal);
    },
    enabled: gameRef !== null,
    staleTime: 5 * 60 * 1000, // 5 minutes — catalog rarely changes mid-session
  });
}
