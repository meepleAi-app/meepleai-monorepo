/**
 * Library Game Detail Layout
 * Issue #5042 — Library + Game Detail Hub
 *
 * Canonical route: /library/[gameId]
 * (replaces /library/games/[gameId] — permanent redirect in next.config.js)
 *
 * Renders PageHeader with contextual tabs (Dettagli · Agente · Toolkit · FAQ)
 * and a primary action for chat. The gameId is dynamic — read from URL params.
 */

'use client';

import { Suspense, type ReactNode } from 'react';

import { useParams, useRouter, useSearchParams } from 'next/navigation';

import { PageHeader } from '@/components/layout/PageHeader';

function LibraryGameHeader() {
  const { gameId } = useParams<{ gameId: string }>();
  const router = useRouter();
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');

  const activeTabId = tab ?? 'details';

  return (
    <PageHeader
      title="Gioco"
      parentHref="/library"
      parentLabel="Libreria"
      tabs={[
        { id: 'details', label: 'Dettagli', href: `/library/${gameId}` },
        { id: 'agent', label: 'Agente', href: `/library/${gameId}?tab=agent` },
        { id: 'toolkit', label: 'Toolkit', href: `/library/${gameId}?tab=toolkit` },
        { id: 'faq', label: 'FAQ', href: `/library/${gameId}?tab=faq` },
      ]}
      activeTabId={activeTabId}
      primaryAction={{
        label: 'Chat con Agente',
        onClick: () => router.push(`/chat/new?gameId=${gameId}`),
      }}
    />
  );
}

export default function LibraryGameDetailLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={<div className="h-14" />}>
        <LibraryGameHeader />
      </Suspense>
      {children}
    </>
  );
}
