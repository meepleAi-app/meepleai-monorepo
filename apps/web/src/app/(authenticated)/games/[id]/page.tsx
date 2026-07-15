/**
 * Game Detail Page — /games/[id]
 *
 * Thin shell: normalizes `params.id` to `string | null` at the page boundary
 * and delegates all rendering to `GameDetailView`.
 *
 * Phase 0.5 contract (docs/frontend/contracts/games-id-hooks.md sez. 2.1):
 *   - rawId comes from useParams, may be undefined pre-hydration
 *   - Normalization: typeof rawId === 'string' && rawId.length > 0 ? rawId : null
 *   - NEVER pass String(rawId) or rawId ?? '' (leads to 'undefined' string)
 *
 * Wave C.1 brownfield migration (Issue #581).
 * Replaces v1 hero + tab-nav-link page (see git history for prior implementation).
 */

'use client';

import { useParams } from 'next/navigation';

import { GameDetailView } from './_components/GameDetailView';

export default function GameDetailPage() {
  const params = useParams<{ id: string }>();
  const rawId = params?.id;
  // ⚠️ Phase 0.5 sez. 2.1: normalize FIRST, NEVER pass undefined downstream
  const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

  return <GameDetailView gameId={gameId} />;
}
