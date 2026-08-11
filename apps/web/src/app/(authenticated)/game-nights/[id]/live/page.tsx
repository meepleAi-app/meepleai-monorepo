/**
 * Game Night Live Hub Page (Issue #1255 cluster game-nights PR 2).
 *
 * Thin wrapper: resolves the dynamic `id` param and delegates rendering
 * to `NightLiveClientView` which integrates `NightLiveHub` (Screen K) +
 * `GameTransitionDialog` (Screen L) from `components/features/game-nights/`.
 *
 * Source mockup: admin-mockups/design_files/sp7-game-night-live.{html,jsx}
 * (mergiato con PR #1250 chiudendo issue #487).
 */

'use client';

import { use } from 'react';

import { NightLiveClientView } from './_components/NightLiveClientView';

export default function GameNightLivePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <NightLiveClientView nightId={id} />;
}
