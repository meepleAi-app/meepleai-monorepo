/**
 * Public Mechanic Card Page — /games/[id]/card (ME-M1.6, Issue #528).
 *
 * Thin shell: normalizes `params.id` (the sharedGameId) to `string | null` at
 * the page boundary and delegates all rendering to `MechanicCardView`.
 *
 * `[id]` IS the sharedGameId — matches the `(authenticated)/games/[id]` group
 * and the backend `PublicCardUrl` (`/games/{sharedGameId}/card`). The route is
 * login-gated by `proxy.ts` (`/games` is a PROTECTED_ROUTE); the view calls
 * `notFound()` when the card endpoint returns 404 (no published card, or the
 * card was suppressed/taken down).
 *
 * Normalization mirrors `(authenticated)/games/[id]/page.tsx`:
 *   const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;
 * NEVER pass `String(rawId)` or `rawId ?? ''` (leads to the 'undefined' string).
 */

'use client';

import { useParams } from 'next/navigation';

import { MechanicCardView } from '@/components/features/mechanic-card';

export default function MechanicCardPage() {
  const params = useParams<{ id: string }>();
  const rawId = params?.id;
  const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

  return <MechanicCardView gameId={gameId} />;
}
