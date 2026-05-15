/**
 * Game Night Detail Page (Issue #951 commit 3).
 *
 * Thin wrapper: resolves the dynamic `id` param and hands rendering to the
 * v2 `GameNightDetailView` page-client which composes the
 * `components/features/game-night-detail/` primitives.
 *
 * Previous monolithic JSX (legacy planning + RSVP + roster) lived inline here
 * up to PR #1171 commits 1-2; commit 3 extracts it under `_components/` to
 * stay aligned with the SP4/SP7 v2 file structure convention.
 */

'use client';

import { use } from 'react';

import { GameNightDetailView } from './_components/GameNightDetailView';

export default function GameNightDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <GameNightDetailView id={id} />;
}
