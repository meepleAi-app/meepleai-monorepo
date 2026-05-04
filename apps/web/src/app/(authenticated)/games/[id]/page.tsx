/**
 * Game Detail Page - /games/[id]
 *
 * Wave C.1 (Issue #581) — brownfield big-bang migration to v2 design.
 * Renders `GameDetailViewV2` (mockup parity with `sp4-game-detail.jsx`).
 *
 * Subroute pages `/games/[id]/{faqs,rules,reviews,sessions,strategies}`
 * are PRESERVED unchanged — the new v2 hero links to them via the FAQ
 * list and rules accordion "view all" CTAs (decision: keep subroute pages).
 */

'use client';

import { useParams } from 'next/navigation';

import { GameDetailViewV2 } from './_components/GameDetailViewV2';

export default function GameDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id ?? '';

  return <GameDetailViewV2 id={id} />;
}
