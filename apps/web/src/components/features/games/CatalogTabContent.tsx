'use client';

import { useCallback, useState } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import { DiscoverSearchBox } from '@/components/features/discover/DiscoverSearchBox';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import { useSharedGames } from '@/hooks/queries';
import { useTranslation } from '@/hooks/useTranslation';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { useGameTitle } from '@/lib/i18n/use-game-title';

const PAGE_SIZE = 24;

/** One catalog game: the title resolves the user's locale (translations) like every game card. */
function CatalogGameCard({ game }: { readonly game: SharedGame }) {
  const { value: title } = useGameTitle(game);
  return (
    <MeepleCard
      entity="game"
      variant="grid"
      title={title}
      subtitle={game.yearPublished ? String(game.yearPublished) : undefined}
      imageUrl={game.coverUrl ?? undefined}
      rating={game.averageRating ?? undefined}
      ratingMax={10}
      href={`/games/${game.id}`}
    />
  );
}

/**
 * `CatalogTabContent` — `/games?tab=catalogo` body.
 *
 * #4990: replaces the "coming soon" placeholder. The shared catalog (published games, searchable,
 * paginated) was reachable only from Library → Aggiungi gioco; here it is browsable, and each card
 * opens `/games/<id>`, which also works for a game not yet in the library.
 *
 * The search lives in `?q=` — the Games hub search box sends the user here with it — so a search
 * survives reload and back navigation.
 */
export function CatalogTabContent() {
  const { t } = useTranslation();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const query = searchParams?.get('q')?.trim() ?? '';

  const [page, setPage] = useState(1);
  // A new search starts from its first page; adjusted during render, no stale page flashes.
  const [pagedQuery, setPagedQuery] = useState(query);
  if (query !== pagedQuery) {
    setPagedQuery(query);
    setPage(1);
  }

  const catalog = useSharedGames(
    { searchTerm: query || undefined, page, pageSize: PAGE_SIZE },
    true,
    true
  );
  const games = catalog.data?.items ?? [];
  const totalPages = catalog.data ? Math.ceil(catalog.data.total / catalog.data.pageSize) : 0;

  const handleSearch = useCallback(
    (value: string) => {
      const params = new URLSearchParams(searchParams?.toString() ?? '');
      params.set('tab', 'catalogo');
      const next = value.trim();
      if (next) params.set('q', next);
      else params.delete('q');
      router.replace(`${pathname}?${params.toString()}`, { scroll: false });
    },
    [pathname, router, searchParams]
  );

  return (
    <div data-testid="games-tab-catalogo" className="flex flex-col gap-6 py-6">
      <DiscoverSearchBox
        value={query}
        onCommit={handleSearch}
        placeholder={t('pages.games.catalog.searchPlaceholder')}
      />

      {catalog.isLoading ? (
        <p className="text-sm text-muted-foreground">{t('pages.games.catalog.loading')}</p>
      ) : catalog.isError ? (
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <span>{t('pages.games.catalog.error')}</span>
          <button
            type="button"
            onClick={() => catalog.refetch()}
            className="font-semibold text-foreground underline"
          >
            {t('pages.discover.common.retry')}
          </button>
        </div>
      ) : games.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          {query ? t('pages.games.catalog.noResults', { query }) : t('pages.discover.common.empty')}
        </p>
      ) : (
        <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
          {games.map(game => (
            <li key={game.id}>
              <CatalogGameCard game={game} />
            </li>
          ))}
        </ul>
      )}

      {totalPages > 1 && (
        <CatalogPagination
          currentPage={page}
          totalPages={totalPages}
          onPageChange={setPage}
          totalResults={catalog.data?.total}
        />
      )}
    </div>
  );
}
