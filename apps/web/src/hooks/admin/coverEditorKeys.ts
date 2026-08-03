/**
 * Admin Cover Picker — TanStack Query key factory (Epic #3470 — Slice 1d-c).
 * Pattern mirrors `mechanicValidationKeys.ts` (nested `as const` tuples).
 */

export const coverEditorKeys = {
  all: ['admin', 'cover-editor'] as const,
  candidates: (gameId: string) => ['admin', 'cover-editor', 'candidates', gameId] as const,
} as const;
