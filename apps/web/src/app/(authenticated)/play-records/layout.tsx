/**
 * Play Records Section Layout
 * Issue #5039 — User Route Consolidation
 *
 * #2158 (Fix #2 codemod): migrated from legacy PageHeader to MiniNavSlot via
 * useMiniNavConfig. The `RecordsListView` page already renders a `MobileHeader`
 * and a sticky `GradientButton` "Nuova partita" that opens `NewPlayRecordSheet`,
 * which superseded the legacy `primaryAction` linking to `/play-records/new`.
 */

'use client';

import { Suspense, type ReactNode } from 'react';

import { useSearchParams } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

function PlayRecordsNav() {
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');
  const activeTabId = tab === 'stats' ? 'stats' : 'records';

  useMiniNavConfig({
    breadcrumb: 'Partite & Statistiche',
    tabs: [
      { id: 'records', label: 'Partite', href: '/play-records' },
      { id: 'stats', label: 'Statistiche', href: '/play-records?tab=stats' },
    ],
    activeTabId,
  });

  return null;
}

export default function PlayRecordsLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={null}>
        <PlayRecordsNav />
      </Suspense>
      {children}
    </>
  );
}
