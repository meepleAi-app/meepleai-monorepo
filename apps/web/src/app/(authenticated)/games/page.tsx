/**
 * `/games` — Games catalog hub (Asse D follow-up P2 #1899, umbrella #1895).
 *
 * Multi-tab hub backing the `Games` sidebar voice per invariante #20 of the
 * GameNight/Session domain model spec
 * (`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`):
 *
 *   > sidebar ha 2 voci game-related: Library (personale) + Games (esplorazione,
 *   > default tab Discover)
 *
 * Tabs:
 *  - `discover` (default) — Discover surface (`DiscoverHub` component).
 *  - `catalogo` — Catalog browser (placeholder MVP — Coming Soon).
 *  - `trending` — Trending games (placeholder MVP — Coming Soon).
 *  - `community` — Community games (placeholder MVP — Coming Soon).
 *
 * Backward compat: the standalone `/discover` route is preserved (existing
 * bookmarks + cross-links). `/games` (no `?tab`) and `/games?tab=invalid`
 * fall back to the default Discover tab.
 *
 * Historical context: this route previously redirected to `/library` per
 * #1521 (the SP4 `library` tab was the only one with a mockup). After Asse C
 * Claude Design handoff (#1895), the catalog/exploration surface (Discover)
 * was promoted to a sidebar voice; this hub restores `/games` as the entry
 * point. The `/games/[id]` game-detail subroute is unaffected (separate route
 * segment).
 */

'use client';

import { Suspense } from 'react';

import { useSearchParams } from 'next/navigation';

import { DiscoverHub } from '@/components/features/discover/DiscoverHub';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

type GamesTab = 'discover' | 'catalogo' | 'trending' | 'community';

const VALID_TABS: ReadonlyArray<GamesTab> = [
  'discover',
  'catalogo',
  'trending',
  'community',
] as const;

function parseTab(raw: string | null): GamesTab {
  if (raw && (VALID_TABS as ReadonlyArray<string>).includes(raw)) return raw as GamesTab;
  return 'discover';
}

const TAB_LABEL: Readonly<Record<GamesTab, string>> = {
  discover: 'Discover',
  catalogo: 'Catalogo',
  trending: 'Trending',
  community: 'Community',
};

function ComingSoonTab({ label }: { label: string }) {
  const slug = label.toLowerCase();
  return (
    <div
      data-testid={`games-tab-${slug}-coming-soon`}
      data-slot={`games-tab-${slug}-coming-soon`}
      className="flex flex-col items-center justify-center gap-3 py-16 text-center"
    >
      <h2 className="text-xl font-semibold">{label}</h2>
      <p className="text-sm text-muted-foreground">
        Funzionalità in arrivo. Disponibile in una release futura.
      </p>
    </div>
  );
}

function GamesHubContent() {
  const searchParams = useSearchParams();
  const activeTab = parseTab(searchParams.get('tab'));

  useMiniNavConfig({
    breadcrumb: 'Games',
    tabs: [
      { id: 'discover', label: TAB_LABEL.discover, href: '/games?tab=discover' },
      { id: 'catalogo', label: TAB_LABEL.catalogo, href: '/games?tab=catalogo' },
      { id: 'trending', label: TAB_LABEL.trending, href: '/games?tab=trending' },
      { id: 'community', label: TAB_LABEL.community, href: '/games?tab=community' },
    ],
    activeTabId: activeTab,
  });

  return (
    <div data-testid="games-hub" data-slot="games-hub" data-active-tab={activeTab}>
      {activeTab === 'discover' && <DiscoverHub pathnameOverride="/games" />}
      {activeTab === 'catalogo' && <ComingSoonTab label={TAB_LABEL.catalogo} />}
      {activeTab === 'trending' && <ComingSoonTab label={TAB_LABEL.trending} />}
      {activeTab === 'community' && <ComingSoonTab label={TAB_LABEL.community} />}
    </div>
  );
}

/**
 * Default export wraps the hub content in a Suspense boundary required by
 * Next.js App Router because `useSearchParams()` opts the page out of SSR
 * static prerendering.
 */
export default function GamesHubPage() {
  return (
    <Suspense fallback={null}>
      <GamesHubContent />
    </Suspense>
  );
}
