/**
 * Play Record Detail Page — Issue #1488 Task 2 (Epic #1475 Phase D).
 *
 * Delegates all rendering to PlayRecordDetailView which provides:
 *   - PlayRecordHeroPodium with full variant matrix (won/tied/cooperative/inprogress/planned)
 *   - ConnectionBar, KpiGrid, Classifica, ScoreBreakdown
 *   - derivePerspective for current-user highlighting
 *   - EC-1..EC-10 edge case handling
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 2
 */
'use client';

import { useParams } from 'next/navigation';

import { PlayRecordDetailView } from '@/components/play-records/PlayRecordDetailView';

export default function PlayRecordDetailsPage() {
  const params = useParams();
  const recordId = typeof params?.id === 'string' ? params.id : '';

  return <PlayRecordDetailView recordId={recordId} />;
}
