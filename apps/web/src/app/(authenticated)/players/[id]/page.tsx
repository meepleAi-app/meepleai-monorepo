/**
 * Player Detail Page — /players/[id]
 *
 * Thin client shell — normalized playerId passed to PlayerDetailView orchestrator.
 * Suspense boundary required: PlayerDetailView uses useSearchParams() which triggers
 * CSR bailout during static prerender without it. Mirror Wave 4 D1 lesson (PR #717).
 *
 * playerId normalization:
 *   - string with length > 0 → pass as-is (URL slug decoded inside orchestrator)
 *   - absent / invalid / undefined → null → FSM maps to 'not-found'
 *
 * Subroutes /players/[id]/{achievements,games,sessions,stats} UNCHANGED.
 *
 * @see Wave 3 /players/[id] (Issue #683)
 */

'use client';

import { Suspense } from 'react';

import { useParams } from 'next/navigation';

import { PlayerDetailView } from './_components/PlayerDetailView';

export default function PlayerDetailPage() {
  const params = useParams<{ id: string }>();
  const rawId = params?.id;
  const playerId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

  return (
    <Suspense>
      <PlayerDetailView playerId={playerId} />
    </Suspense>
  );
}
