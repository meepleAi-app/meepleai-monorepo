/**
 * Library Game Detail Layout
 * Issue #5042 — Library + Game Detail Hub
 *
 * Canonical route: /library/[gameId]
 * (replaces /library/[gameId] — permanent redirect in next.config.js)
 *
 * Renders PageHeader with contextual tabs (Dettagli · Agente · Toolkit · FAQ)
 * and a primary action for chat. The gameId is dynamic — read from URL params.
 *
 * #1816 P2-2 — h1 + document.title resolve the game name from
 * `useLibraryGameDetail`. Three semantic states surfaced to the header:
 *   - loading: t('pages.library.gameDetail.h1.loading')
 *   - loaded:  game.gameTitle
 *   - 404:     t('pages.library.gameDetail.h1.notFound')
 * The page itself also calls `useLibraryGameDetail` — React Query dedupes via
 * the shared `libraryKeys.gameDetail(gameId)` key, so no duplicate fetch.
 */

'use client';

import { Suspense, useEffect, type ReactNode } from 'react';

import { useParams, useRouter, useSearchParams } from 'next/navigation';

import { PageHeader } from '@/components/layout/PageHeader';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';

// Exported for unit testing the 3-state h1/document.title resolution (#1816 P2-2)
// without forcing tests through the Suspense boundary.
export function LibraryGameHeader() {
  const { gameId } = useParams<{ gameId: string }>();
  const router = useRouter();
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');
  const { t } = useTranslation();

  const { data: gameDetail, isLoading } = useLibraryGameDetail(gameId);

  // 3-state resolution for header h1 + browser document.title.
  // The PageHeader component renders `title` inside an h1 element.
  let headerTitle: string;
  let documentTitle: string;
  if (isLoading) {
    headerTitle = t('pages.library.gameDetail.h1.loading');
    documentTitle = t('pages.library.gameDetail.documentTitle.loading');
  } else if (gameDetail?.gameTitle) {
    headerTitle = gameDetail.gameTitle;
    documentTitle = t('pages.library.gameDetail.documentTitle.format', {
      gameName: gameDetail.gameTitle,
    });
  } else {
    headerTitle = t('pages.library.gameDetail.h1.notFound');
    documentTitle = t('pages.library.gameDetail.documentTitle.notFound');
  }

  // Document title — restore prior value on unmount so the browser tab does
  // not retain the game name when navigating to a non-library surface.
  useEffect(() => {
    const previous = document.title;
    document.title = documentTitle;
    return () => {
      document.title = previous;
    };
  }, [documentTitle]);

  const activeTabId = tab ?? 'details';

  return (
    <PageHeader
      title={headerTitle}
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
