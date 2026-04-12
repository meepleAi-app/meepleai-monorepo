'use client';

import { useEffect, useMemo, useState } from 'react';

import { useRouter } from 'next/navigation';

import { useLibrary, useLibraryStats } from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { useRecentsStore } from '@/stores/use-recents';

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

// ─── Adapters: UserLibraryEntry → section view models ─────────────────────

function entryToPersonalGame(e: UserLibraryEntry): PersonalLibraryGame {
  return {
    id: e.gameId,
    title: e.gameTitle,
    subtitle: e.gamePublisher ?? undefined,
    imageUrl: e.gameImageUrl ?? e.gameIconUrl ?? undefined,
    rating: e.averageRating ?? undefined,
  };
}

function entryToCatalogGame(e: UserLibraryEntry): CatalogGame {
  return {
    id: e.gameId,
    title: e.gameTitle,
    subtitle: e.gamePublisher ?? undefined,
    imageUrl: e.gameImageUrl ?? e.gameIconUrl ?? undefined,
    rating: e.averageRating ?? undefined,
  };
}

function entryToWishlistGame(e: UserLibraryEntry): WishlistGame {
  return {
    id: e.gameId,
    title: e.gameTitle,
    subtitle: e.gamePublisher ?? undefined,
    imageUrl: e.gameImageUrl ?? e.gameIconUrl ?? undefined,
    rating: e.averageRating ?? undefined,
  };
}

function entryToContinueGame(e: UserLibraryEntry): ContinuePlayingGame {
  return {
    id: e.gameId,
    title: e.gameTitle,
    subtitle: e.gamePublisher ?? undefined,
    imageUrl: e.gameImageUrl ?? e.gameIconUrl ?? undefined,
    rating: e.averageRating ?? undefined,
    lastPlayedLabel: e.stateChangedAt ? new Date(e.stateChangedAt).toLocaleDateString() : '—',
  };
}

/**
 * Library Hub client — carousel-landing layout wired to real library data.
 */
export function LibraryHub() {
  const router = useRouter();

  const { data: stats } = useLibraryStats();
  const { data: libraryData } = useLibrary({
    page: 1,
    pageSize: 50,
    sortBy: 'addedAt',
    sortDescending: true,
  });

  // Section data derived from library entries
  // Wrapped in useMemo so the array reference is stable across renders;
  // otherwise it would invalidate every downstream useMemo dependency.
  const allEntries: UserLibraryEntry[] = useMemo(() => libraryData?.items ?? [], [libraryData]);

  const personalGames = useMemo<PersonalLibraryGame[]>(
    () => allEntries.filter(e => e.currentState !== 'Wishlist').map(entryToPersonalGame),
    [allEntries]
  );

  const catalogGames = useMemo<CatalogGame[]>(
    () =>
      allEntries
        .filter(e => !e.isPrivateGame && e.currentState !== 'Wishlist')
        .map(entryToCatalogGame),
    [allEntries]
  );

  const wishlistGames = useMemo<WishlistGame[]>(
    () => allEntries.filter(e => e.currentState === 'Wishlist').map(entryToWishlistGame),
    [allEntries]
  );

  // "Continua a giocare" — favorites sorted by stateChangedAt, limited to 10.
  // Play session history is not available in the library list endpoint;
  // favorites ordered by last state change is the best available approximation.
  const continueGames = useMemo<ContinuePlayingGame[]>(
    () =>
      allEntries
        .filter(e => e.isFavorite && e.currentState !== 'Wishlist')
        .slice(0, 10)
        .map(entryToContinueGame),
    [allEntries]
  );

  const headerStats = useMemo(
    () => ({
      owned: (stats?.totalGames ?? 0) - (stats?.wishlistCount ?? 0),
      catalog: stats?.ownedCount ?? 0,
      wishlist: stats?.wishlistCount ?? 0,
    }),
    [stats]
  );

  const [activeFilter, setActiveFilter] = useState<LibraryFilterKey>('all');
  const [activeView, setActiveView] = useState<LibraryViewMode>('carousels');

  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        {
          id: 'wishlist',
          label: 'Wishlist',
          href: '/library/wishlist',
          count: headerStats.wishlist,
        },
      ],
      activeTabId: 'hub',
      primaryAction: {
        label: 'Aggiungi gioco',
        icon: '＋',
        onClick: () => router.push('/library?action=add'),
      },
    }),
    [router, headerStats.wishlist]
  );
  useMiniNavConfig(miniNavConfig);

  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-library-hub',
      entity: 'game',
      title: 'Library',
      href: '/library',
    });
  }, []);

  const handleAddGame = () => {
    router.push('/library?action=add');
  };

  return (
    <div className="mx-auto max-w-[1440px] p-7 pb-12">
      <LibraryHeader stats={headerStats} />
      <LibraryFilterBar
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        activeView={activeView}
        onViewChange={setActiveView}
        sortLabel="Ultimo aggiunto"
        onSortClick={() => {}}
      />
      <ContinuePlayingSection games={continueGames} />
      <PersonalLibrarySection
        games={personalGames}
        totalCount={headerStats.owned}
        onAddGame={handleAddGame}
      />
      <CatalogCarouselSection games={catalogGames} totalCount={headerStats.catalog} />
      <WishlistCarouselSection games={wishlistGames} totalCount={headerStats.wishlist} />
    </div>
  );
}
