/**
 * Library Game Detail Layout
 * Issue #5042 — Library + Game Detail Hub
 *
 * Canonical route: /library/[gameId]
 *
 * #2158 (Fix #2 codemod): migrated from legacy PageHeader to MiniNavSlot via
 * useMiniNavConfig. The game title is already rendered as a big hero by
 * `GameDetailDesktop` (`title={game?.gameTitle ...}`), so the PageHeader's
 * h1 was duplicating it. The "Chat con Agente" primaryAction was likewise
 * redundant with the `Agente` tab in the same nav.
 *
 * #1816 P2-2 — `document.title` still resolves the game name from
 * `useLibraryGameDetail` (3-state: loading / loaded / 404). The page itself
 * also calls `useLibraryGameDetail` — React Query dedupes via the shared
 * `libraryKeys.gameDetail(gameId)` key, so no duplicate fetch.
 */

'use client';

import { Suspense, useEffect, type ReactNode } from 'react';

import { useParams, useSearchParams } from 'next/navigation';

import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useTranslation } from '@/hooks/useTranslation';

// Exported for unit testing the 3-state breadcrumb/document.title resolution
// (#1816 P2-2) without forcing tests through the Suspense boundary.
export function LibraryGameHeader() {
  const { gameId } = useParams<{ gameId: string }>();
  const searchParams = useSearchParams();
  const tab = searchParams?.get('tab');
  const { t } = useTranslation();

  const { data: gameDetail, isLoading } = useLibraryGameDetail(gameId);

  // 3-state resolution for breadcrumb crumb + browser document.title.
  let crumbName: string;
  let documentTitle: string;
  if (isLoading) {
    crumbName = t('pages.library.gameDetail.h1.loading');
    documentTitle = t('pages.library.gameDetail.documentTitle.loading');
  } else if (gameDetail?.gameTitle) {
    crumbName = gameDetail.gameTitle;
    documentTitle = t('pages.library.gameDetail.documentTitle.format', {
      gameName: gameDetail.gameTitle,
    });
  } else {
    crumbName = t('pages.library.gameDetail.h1.notFound');
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

  useMiniNavConfig({
    breadcrumb: `Libreria · ${crumbName}`,
    tabs: [
      { id: 'details', label: 'Dettagli', href: `/library/${gameId}` },
      { id: 'agent', label: 'Agente', href: `/library/${gameId}?tab=agent` },
      { id: 'toolkit', label: 'Toolkit', href: `/library/${gameId}?tab=toolkit` },
      { id: 'faq', label: 'FAQ', href: `/library/${gameId}?tab=faq` },
    ],
    activeTabId,
  });

  return null;
}

export default function LibraryGameDetailLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={null}>
        <LibraryGameHeader />
      </Suspense>
      {children}
    </>
  );
}
