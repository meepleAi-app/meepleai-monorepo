/**
 * Play Records Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5045 — Play Records + PageHeader
 *
 * Renders PageHeader with tabs and primary action for the /play-records section.
 */

'use client';

import { Suspense, type ReactNode } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { PageHeader } from '@/components/layout/PageHeader';

function PlayRecordsHeader() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');

  const activeTabId = tab === 'stats' ? 'stats' : 'records';

  return (
    <PageHeader
      title="Partite & Statistiche"
      tabs={[
        { id: 'records', label: 'Partite', href: '/play-records' },
        { id: 'stats', label: 'Statistiche', href: '/play-records?tab=stats' },
      ]}
      activeTabId={activeTabId}
      primaryAction={{
        label: 'Nuova Partita',
        onClick: () => router.push('/play-records/new'),
      }}
    />
  );
}

export default function PlayRecordsLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={<div className="h-14" />}>
        <PlayRecordsHeader />
      </Suspense>
      {children}
    </>
  );
}
