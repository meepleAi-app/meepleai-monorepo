/**
 * Library Game Detail Layout
 * Issue #5042 — Library + Game Detail Hub
 *
 * Canonical route: /library/[gameId]
 *
 * #2158 (Fix #2 codemod + visual-smoke follow-up): the legacy `PageHeader`
 * (h1 + tabs + primaryAction) was the pre-Asse-B header. After codemod the
 * first attempt was to register the breadcrumb + 4 tabs via `useMiniNavConfig`,
 * but visual smoke showed that the page already renders:
 *   - the game title as a big hero (`GameDetailDesktop`)
 *   - its own 5-tab nav (INFO · AGENTE · TOOLKIT · HOUSE RULES · PARTITE)
 * So the MiniNavSlot strip was duplicating navigation. Same pattern as
 * `/library` (LibraryHub): the page owns its own tabs and title — the
 * layout no longer consumes MiniNavSlot.
 *
 * #1816 P2-2 — `document.title` still resolves the game name from
 * `useLibraryGameDetail` (3-state: loading / loaded / 404). The page itself
 * also calls `useLibraryGameDetail` — React Query dedupes via the shared
 * `libraryKeys.gameDetail(gameId)` key, so no duplicate fetch.
 */

'use client';

import { Suspense, useEffect, type ReactNode } from 'react';

import { useParams } from 'next/navigation';

import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';

// Exported for unit testing the 3-state document.title resolution (#1816 P2-2)
// without forcing tests through the Suspense boundary.
export function LibraryGameHeader() {
  const { gameId } = useParams<{ gameId: string }>();
  const { t } = useTranslation();

  const { data: gameDetail, isLoading } = useLibraryGameDetail(gameId);

  // 3-state resolution for browser document.title (a11y + SEO).
  let documentTitle: string;
  if (isLoading) {
    documentTitle = t('pages.library.gameDetail.documentTitle.loading');
  } else if (gameDetail?.gameTitle) {
    documentTitle = t('pages.library.gameDetail.documentTitle.format', {
      gameName: gameDetail.gameTitle,
    });
  } else {
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
