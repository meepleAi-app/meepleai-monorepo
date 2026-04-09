'use client';

import { useEffect, useMemo, useState } from 'react';

import { useRouter } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useCardHand } from '@/stores/use-card-hand';

import { CatalogCarouselSection, type CatalogGame } from './sections/CatalogCarouselSection';
import {
  ContinuePlayingSection,
  type ContinuePlayingGame,
} from './sections/ContinuePlayingSection';
import {
  LibraryFilterBar,
  type LibraryFilterKey,
  type LibraryViewMode,
} from './sections/LibraryFilterBar';
import { LibraryHeader } from './sections/LibraryHeader';
import {
  PersonalLibrarySection,
  type PersonalLibraryGame,
} from './sections/PersonalLibrarySection';
import { WishlistCarouselSection, type WishlistGame } from './sections/WishlistCarouselSection';

/**
 * Phase 3 Library Hub client — new carousel-landing layout.
 * Reads data from TODO(Phase 4) — useLibraryStore — for now, empty defaults.
 */
export function LibraryHub() {
  const router = useRouter();
  const drawCard = useCardHand(s => s.drawCard);

  // TODO(Phase 4): wire to useLibraryStore once it exposes Phase 3 fields.
  // For now, sections with no data self-hide and the Personal carousel
  // renders only the "Aggiungi gioco" ghost card.
  const continueGames: ContinuePlayingGame[] = [];
  const personalGames: PersonalLibraryGame[] = [];
  const catalogGames: CatalogGame[] = [];
  const wishlistGames: WishlistGame[] = [];
  const stats = { owned: 0, catalog: 0, wishlist: 0 };

  const [activeFilter, setActiveFilter] = useState<LibraryFilterKey>('all');
  const [activeView, setActiveView] = useState<LibraryViewMode>('carousels');

  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal', count: stats.owned },
        { id: 'catalogo', label: 'Catalogo', href: '/library?tab=catalogo', count: stats.catalog },
        { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist', count: stats.wishlist },
      ],
      activeTabId: 'hub',
      primaryAction: {
        label: 'Aggiungi gioco',
        icon: '＋',
        onClick: () => router.push('/library?action=add'),
      },
    }),
    [router, stats.owned, stats.catalog, stats.wishlist]
  );
  useMiniNavConfig(miniNavConfig);

  useEffect(() => {
    drawCard({
      id: 'section-library-hub',
      entity: 'game',
      title: 'Library',
      href: '/library',
    });
  }, [drawCard]);

  const handleAddGame = () => {
    router.push('/library?action=add');
  };

  const handleSortClick = () => {
    // TODO(Phase 4): open sort menu
  };

  return (
    <div className="mx-auto max-w-[1440px] p-7 pb-12">
      <LibraryHeader stats={stats} />
      <LibraryFilterBar
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        activeView={activeView}
        onViewChange={setActiveView}
        sortLabel="Ultimo giocato"
        onSortClick={handleSortClick}
      />
      <ContinuePlayingSection games={continueGames} />
      <PersonalLibrarySection
        games={personalGames}
        totalCount={stats.owned}
        onAddGame={handleAddGame}
      />
      <CatalogCarouselSection games={catalogGames} totalCount={stats.catalog} />
      <WishlistCarouselSection games={wishlistGames} totalCount={stats.wishlist} />
    </div>
  );
}
