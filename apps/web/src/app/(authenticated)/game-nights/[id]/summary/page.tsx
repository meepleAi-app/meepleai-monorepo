/**
 * Game Night Summary Page (Issue #1255 cluster game-nights PR 2).
 *
 * Thin wrapper: resolves the dynamic `id` param and delegates rendering
 * to `NightSummaryClientView` which integrates `NightSummaryView` (Screen M)
 * from `components/features/game-nights/summary/`.
 *
 * Source mockup: admin-mockups/design_files/sp7-game-night-summary.{html,jsx}
 * (mergiato con PR #1250 chiudendo issue #487).
 */

'use client';

import { use } from 'react';

import { NightSummaryClientView } from './_components/NightSummaryClientView';

export default function GameNightSummaryPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <NightSummaryClientView nightId={id} />;
}
