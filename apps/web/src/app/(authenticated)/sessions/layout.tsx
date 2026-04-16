/**
 * Sessions Section Layout
 * Issue #5045 — Sessions + PageHeader
 *
 * Renders PageHeader with tabs and primary action for the /sessions section.
 * Tabs: Attive · Storico
 */

'use client';

import { Suspense, type ReactNode } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { PageHeader } from '@/components/layout/PageHeader';

function SessionsHeader() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');

  const activeTabId = tab === 'history' ? 'history' : 'active';

  return (
    <PageHeader
      title="Sessioni"
      tabs={[
        { id: 'active', label: 'Attive', href: '/sessions' },
        { id: 'history', label: 'Storico', href: '/sessions?tab=history' },
      ]}
      activeTabId={activeTabId}
      primaryAction={{
        label: 'Nuova Sessione',
        onClick: () => router.push('/sessions/new'),
      }}
    />
  );
}

export default function SessionsLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={<div className="h-14" />}>
        <SessionsHeader />
      </Suspense>
      {children}
    </>
  );
}
