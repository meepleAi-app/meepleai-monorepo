/**
 * Sessions Section Layout
 *
 * #2158 (Fix #2 codemod): migrated from legacy PageHeader to MiniNavSlot via
 * useMiniNavConfig. The title "Sessioni" + CTA "Nuova Sessione" already live
 * in `SessionsHero` (rendered by `SessionsLibraryView` in `page.tsx`); the
 * legacy PageHeader was duplicating both.
 *
 * Tabs: Attive · Storico
 */

'use client';

import { Suspense, type ReactNode } from 'react';

import { useSearchParams } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

function SessionsNav() {
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');
  const activeTabId = tab === 'history' ? 'history' : 'active';

  useMiniNavConfig({
    breadcrumb: 'Sessioni',
    tabs: [
      { id: 'active', label: 'Attive', href: '/sessions' },
      { id: 'history', label: 'Storico', href: '/sessions?tab=history' },
    ],
    activeTabId,
  });

  return null;
}

export default function SessionsLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={null}>
        <SessionsNav />
      </Suspense>
      {children}
    </>
  );
}
