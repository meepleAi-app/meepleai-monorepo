'use client';

/**
 * Wishlist Page — orchestrator (Issue #3007, Task B6).
 *
 * Assembles the pure filter/sort/stats pipeline (`_lib/wishlist-filters`,
 * Task B1) with the presentational components built in B3-B5
 * (`WishlistStats`, `WishlistToolbar`, `MeepleWishlistCard`,
 * `AddToWishlistDialog`) into the full `/library/wishlist` experience. The
 * wishlist endpoint returns a plain array with no search/filter/sort
 * support, so the entire experience runs client-side over the full list.
 *
 * Mockup: `admin-mockups/design_files/sp4-library-wishlist-ui.jsx`.
 */

import { useMemo, useState, type JSX } from 'react';

import { AlertCircle, PlusCircle } from 'lucide-react';
import Link from 'next/link';

import { HubPageContainer } from '@/components/layout/PageContainer';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { AddToWishlistDialog } from '@/components/wishlist/AddToWishlistDialog';
import { MeepleWishlistCard } from '@/components/wishlist/MeepleWishlistCard';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useRemoveFromWishlist, useWishlist } from '@/hooks/queries/useWishlist';
import { useTranslation } from '@/hooks/useTranslation';
import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';
import { cn } from '@/lib/utils';

import { WishlistStats } from './_components/WishlistStats';
import { WishlistToolbar } from './_components/WishlistToolbar';
import {
  computeStats,
  countActiveFilters,
  filterItems,
  sortItems,
  type Priority,
  type WishlistFilterState,
} from './_lib/wishlist-filters';

// ============================================================================
// Constants
// ============================================================================

/** Batch size for the library fetch that resolves the gameId → gameTitle map. */
const LIBRARY_BATCH_LIMIT = 500;

const DEFAULT_FILTER_STATE: WishlistFilterState = {
  search: '',
  priorities: [],
  sort: 'priority',
};

interface LibraryTab {
  id: 'library' | 'wishlist';
  href: string;
}

const LIBRARY_TABS: LibraryTab[] = [
  { id: 'library', href: '/library' },
  { id: 'wishlist', href: '/library/wishlist' },
];

// ============================================================================
// Page
// ============================================================================

export default function WishlistPage(): JSX.Element {
  const { t } = useTranslation();

  const [filterState, setFilterState] = useState<WishlistFilterState>(DEFAULT_FILTER_STATE);
  /** The item currently open in the edit dialog, or null when no edit dialog is open. */
  const [editingItem, setEditingItem] = useState<WishlistItemDto | null>(null);

  const { data, isLoading, isError, refetch } = useWishlist();
  const { data: libraryData } = useLibrary({ pageSize: LIBRARY_BATCH_LIMIT });
  const { mutate: removeItem } = useRemoveFromWishlist();

  // Build a gameId → gameTitle lookup from the user's library, used as a
  // fallback when a wishlist item has no denormalized `gameName`.
  const gameNameMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const entry of libraryData?.items ?? []) {
      map.set(entry.gameId, entry.gameTitle);
    }
    return map;
  }, [libraryData?.items]);

  const allItems = useMemo(() => data ?? [], [data]);
  const stats = useMemo(() => computeStats(allItems), [allItems]);
  const filtered = useMemo(
    () => sortItems(filterItems(allItems, filterState, gameNameMap), filterState.sort),
    [allItems, filterState, gameNameMap]
  );
  const activeFilterCount = countActiveFilters(filterState);

  const hasItems = allItems.length > 0;
  const noFilteredResults = hasItems && filtered.length === 0 && activeFilterCount > 0;

  const handleClearAll = () => setFilterState(DEFAULT_FILTER_STATE);

  const handleFilterPriority = (priority: Priority) => {
    setFilterState(prev =>
      prev.priorities.includes(priority)
        ? prev
        : { ...prev, priorities: [...prev.priorities, priority] }
    );
  };

  return (
    <HubPageContainer className="flex flex-col gap-6">
      {/* breadcrumb */}
      <nav
        aria-label={t('pages.library.wishlist.hero.breadcrumbAria')}
        className="flex items-center gap-1.5 text-sm text-muted-foreground"
      >
        <Link href="/library" className="hover:text-foreground">
          {t('pages.library.wishlist.hero.breadcrumbLibrary')}
        </Link>
        <span aria-hidden="true">›</span>
        <span className="font-medium text-foreground">
          {t('pages.library.wishlist.hero.breadcrumbWishlist')}
        </span>
      </nav>

      {/* hero */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold text-foreground sm:text-3xl">
            {t('pages.library.wishlist.hero.title')}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {t('pages.library.wishlist.hero.subtitle')}
          </p>
        </div>
        <AddToWishlistDialog
          mode="add"
          trigger={
            <Button type="button" className="gap-1.5">
              <PlusCircle className="h-4 w-4" aria-hidden="true" />
              {t('pages.library.wishlist.hero.addCta')}
            </Button>
          }
        />
      </div>

      {/* library / wishlist tabs */}
      <nav
        aria-label={t('pages.library.wishlist.hero.tabsAria')}
        className="flex gap-1 overflow-x-auto border-b border-border"
      >
        {LIBRARY_TABS.map(tab => (
          <Link
            key={tab.id}
            href={tab.href}
            aria-current={tab.id === 'wishlist' ? 'page' : undefined}
            className={cn(
              'flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2 text-sm font-medium transition-colors',
              tab.id === 'wishlist'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
          >
            {t(`pages.library.wishlist.hero.tab${tab.id === 'library' ? 'Library' : 'Wishlist'}`)}
          </Link>
        ))}
      </nav>

      <WishlistStats stats={stats} />

      {!isLoading && !isError && hasItems && (
        <WishlistToolbar
          state={filterState}
          onChange={setFilterState}
          priorityCounts={stats.priorityCounts}
          totalCount={stats.total}
          resultCount={filtered.length}
          onClearAll={handleClearAll}
        />
      )}

      {/* loading */}
      {isLoading && (
        <div
          role="status"
          aria-busy="true"
          aria-label={t('pages.library.wishlist.loading.ariaLabel')}
          className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
        >
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-48 w-full rounded-xl" />
          ))}
        </div>
      )}

      {/* error */}
      {!isLoading && isError && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="flex-1">{t('pages.library.wishlist.error.message')}</span>
          <Button type="button" variant="outline" size="sm" onClick={() => void refetch()}>
            {t('pages.library.wishlist.error.retry')}
          </Button>
        </div>
      )}

      {/* empty: no items at all */}
      {!isLoading && !isError && !hasItems && (
        <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-card px-6 py-16 text-center">
          <span aria-hidden="true" className="text-4xl">
            ❤️
          </span>
          <h2 className="text-lg font-semibold text-foreground">
            {t('pages.library.wishlist.empty.noItems.title')}
          </h2>
          <p className="max-w-md text-sm text-muted-foreground">
            {t('pages.library.wishlist.empty.noItems.body')}
          </p>
          <AddToWishlistDialog
            mode="add"
            trigger={<Button type="button">{t('pages.library.wishlist.empty.noItems.cta')}</Button>}
          />
        </div>
      )}

      {/* empty: filters exclude everything */}
      {!isLoading && !isError && noFilteredResults && (
        <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-card px-6 py-16 text-center">
          <span aria-hidden="true" className="text-4xl">
            🔍
          </span>
          <h2 className="text-lg font-semibold text-foreground">
            {t('pages.library.wishlist.empty.filtered.title')}
          </h2>
          <p className="max-w-md text-sm text-muted-foreground">
            {t('pages.library.wishlist.empty.filtered.body')}
          </p>
          <Button type="button" variant="outline" onClick={handleClearAll}>
            {t('pages.library.wishlist.empty.filtered.cta')}
          </Button>
        </div>
      )}

      {/* grid */}
      {!isLoading && !isError && hasItems && !noFilteredResults && (
        <div
          aria-label={t('pages.library.wishlist.card.gridAria')}
          className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
        >
          {filtered.map(item => (
            <MeepleWishlistCard
              key={item.id}
              item={item}
              gameName={item.gameName ?? gameNameMap.get(item.gameId)}
              onRemove={id => removeItem(id)}
              onEdit={setEditingItem}
              onFilterPriority={handleFilterPriority}
            />
          ))}
        </div>
      )}

      {/*
        Edit dialog: conditionally rendered (rather than always-mounted with a
        controlled `open` toggle) and keyed by item id. `AddToWishlistDialog`
        only re-derives its internal form state (`buildInitialGame`, priority,
        targetPrice, notes) on mount / on its own `handleOpenChange(true)` —
        which Radix's `Dialog` invokes for *internal* open transitions
        (trigger click, Escape, overlay), not for a parent-driven `open` prop
        flip. Keying by `editingItem.id` forces a fresh mount (and therefore a
        fresh prefill) every time a different item is opened for editing.
      */}
      {editingItem && (
        <AddToWishlistDialog
          key={editingItem.id}
          mode="edit"
          item={editingItem}
          open
          onOpenChange={next => {
            if (!next) setEditingItem(null);
          }}
        />
      )}
    </HubPageContainer>
  );
}
