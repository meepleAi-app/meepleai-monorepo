'use client';

/**
 * CatalogSearchStep — Step 1b of AddGameDrawer
 * Issue #5169 — Search shared catalog and add game to personal library
 *
 * Provides:
 * - Debounced (300ms) search input
 * - Grid of selectable game cards
 * - Loading skeletons
 * - Pagination (CatalogPagination)
 * - "Already in library" disabled state via useGameInLibraryStatus
 * - Calls POST /api/v1/library/games/{gameId} on select via useAddGameToLibrary
 * - On success → calls onSelect(gameId, gameName) to advance wizard to PDF step
 *
 * F2.2 #1974 (audit 2026-06-07) T1+T2:
 *   T1 — all hardcoded EN strings moved to `pages.library.addGame.catalog.*`
 *        i18n keys (it.json + en.json) so the catalog step reads in Italian
 *        for the IT-default user base.
 *   T2 — empty-results state upgraded from a single muted line to a
 *        first-class block with heading + subtitle so an empty search
 *        feels like a real state, not a render glitch.
 */

import { useState, useEffect, useRef, useCallback } from 'react';

import { ArrowLeft, Search, X } from 'lucide-react';
import Image from 'next/image';

import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import { toast } from '@/components/layout';
import { Button } from '@/components/ui/primitives/button';
import { useSharedGames } from '@/hooks/queries';
import { useBatchGameStatus } from '@/hooks/queries/useBatchGameStatus';
import { useAddGameToLibrary } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import type { SharedGame } from '@/lib/api';

// ─── Constants ────────────────────────────────────────────────────────────────

const PAGE_SIZE = 9;
const DEBOUNCE_MS = 300;

// ─── Single selectable card ───────────────────────────────────────────────────

interface SelectableGameCardProps {
  game: SharedGame;
  onSelect: (gameId: string, gameName: string) => void;
  isSelecting: boolean;
  /**
   * #2275 — Library status passed in from the parent's batched
   * useBatchGameStatus call instead of a per-card useGameInLibraryStatus
   * (avoids N+1 requests when the grid renders ≤9 cards per page).
   */
  inLibrary: boolean;
  labels: {
    noImage: string;
    inLibraryBadge: string;
    alreadyInLibrary: string;
    selectCta: string;
  };
  /**
   * #2269 P0-2 (M2) — Optional handler invoked when the user clicks the
   * surface of an already-in-library card (not the disabled action button).
   * When present, the parent surfaces a non-intrusive inline alert.
   */
  onBlocked?: (game: SharedGame) => void;
}

function SelectableGameCard({
  game,
  onSelect,
  isSelecting,
  inLibrary,
  labels,
  onBlocked,
}: SelectableGameCardProps) {
  const isBlockable = inLibrary && Boolean(onBlocked);

  return (
    <div
      data-testid={`catalog-card-${game.id}`}
      onClick={isBlockable ? () => onBlocked?.(game) : undefined}
      role={isBlockable ? 'button' : undefined}
      tabIndex={isBlockable ? 0 : undefined}
      onKeyDown={
        isBlockable
          ? e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onBlocked?.(game);
              }
            }
          : undefined
      }
      className={[
        'flex flex-col rounded-lg border border-border/50 overflow-hidden',
        'transition-shadow',
        inLibrary ? 'opacity-60' : 'hover:shadow-md',
        isBlockable ? 'cursor-pointer' : '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {/* Thumbnail */}
      <div className="relative aspect-video bg-muted overflow-hidden">
        {game.thumbnailUrl ? (
          <Image
            src={game.thumbnailUrl}
            alt={game.title}
            fill
            className="object-cover"
            sizes="(max-width: 640px) 100vw, 33vw"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-muted-foreground text-xs">
            {labels.noImage}
          </div>
        )}
        {inLibrary && (
          <div
            data-testid={`catalog-card-${game.id}-in-library-badge`}
            // #2269 P0-4 — token canonical: bg-green-600 → arbitrary
            // hsl(var(--c-success)). Arbitrary bg-[hsl(...)] is explicitly
            // in the DS-15 .e-bg exemption list for `text-white`.
            className="absolute top-1.5 right-1.5 bg-[hsl(var(--c-success))] text-white text-xs font-medium px-1.5 py-0.5 rounded"
          >
            {labels.inLibraryBadge}
          </div>
        )}
      </div>

      {/* Info */}
      <div className="p-3 flex flex-col gap-2 flex-1">
        <p className="font-medium text-sm leading-tight line-clamp-2" title={game.title}>
          {game.title}
        </p>
        {game.yearPublished > 0 && (
          <p className="text-xs text-muted-foreground">{game.yearPublished}</p>
        )}
        <Button
          size="sm"
          variant={inLibrary ? 'outline' : 'default'}
          disabled={inLibrary || isSelecting}
          // #2269 P0-2 (M2) — stop propagation so the outer card onClick
          // (blocked-alert trigger) never doubles up with the button's own
          // handler when a user clicks the disabled CTA directly.
          onClick={e => {
            e.stopPropagation();
            onSelect(game.id, game.title);
          }}
          data-testid={`catalog-card-${game.id}-select-btn`}
          className="mt-auto w-full"
        >
          {inLibrary ? labels.alreadyInLibrary : labels.selectCta}
        </Button>
      </div>
    </div>
  );
}

// ─── Skeleton ─────────────────────────────────────────────────────────────────

function CardSkeleton() {
  return (
    <div className="flex flex-col rounded-lg border border-border/50 overflow-hidden animate-pulse">
      <div className="aspect-video bg-muted" />
      <div className="p-3 space-y-2">
        <div className="h-4 bg-muted rounded w-3/4" />
        <div className="h-3 bg-muted rounded w-1/4" />
        <div className="h-8 bg-muted rounded mt-2" />
      </div>
    </div>
  );
}

// ─── Empty state (F2.2 T2) ────────────────────────────────────────────────────

interface CatalogSearchEmptyStateProps {
  heading: string;
  subtitle: string;
  emptyTemplate: string;
  manualCtaLabel: string;
  onGoToManual?: () => void;
}

function CatalogSearchEmptyState({
  heading,
  subtitle,
  emptyTemplate,
  manualCtaLabel,
  onGoToManual,
}: CatalogSearchEmptyStateProps) {
  return (
    <div
      className="col-span-full flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-muted/20 px-6 py-10 text-center"
      data-testid="catalog-search-empty"
      role="status"
    >
      <span aria-hidden="true" className="text-3xl">
        🔎
      </span>
      <h3 className="text-base font-semibold text-foreground">{heading}</h3>
      <p className="text-sm text-muted-foreground">{emptyTemplate}</p>
      <p className="text-xs text-muted-foreground">{subtitle}</p>
      {onGoToManual && (
        <Button
          variant="default"
          size="sm"
          className="mt-3"
          onClick={onGoToManual}
          data-testid="catalog-search-empty-manual-cta"
        >
          {manualCtaLabel}
        </Button>
      )}
    </div>
  );
}

// ─── Main step ────────────────────────────────────────────────────────────────

interface CatalogSearchStepProps {
  /**
   * Called when user selects a game and it is successfully added to library.
   * Advance the wizard with gameId — name is internal (used here for toasts).
   */
  onSelect: (gameId: string) => void;
  /** Go back to Step 0 (choice) */
  onBack: () => void;
  /**
   * #2269 P0-1 (M1) — Optional bridge to the manual step. When provided and
   * the catalog returns 0 results, the empty state exposes an "Add manually"
   * CTA that invokes this callback so the user is not stuck on a dead-end.
   */
  onGoToManual?: () => void;
  /**
   * #2269 P0-2 (M2) — Optional handler invoked from the blocked-alert CTA
   * when the user wants to jump to an already-owned game's detail page.
   * Keeping the routing/close concern at the parent boundary mirrors the
   * pattern used by `onSelect`.
   */
  onNavigateToGame?: (gameId: string) => void;
}

const BLOCKED_AUTO_DISMISS_MS = 4500;

export function CatalogSearchStep({
  onSelect,
  onBack,
  onGoToManual,
  onNavigateToGame,
}: CatalogSearchStepProps) {
  const { t } = useTranslation();
  const [rawSearch, setRawSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [page, setPage] = useState(1);
  const [selectingId, setSelectingId] = useState<string | null>(null);
  // #2269 P0-2 (M2) — non-intrusive inline alert state for already-in-library
  // clicks. `null` = hidden; `{id,title}` = visible. Auto-dismisses after
  // BLOCKED_AUTO_DISMISS_MS unless the user clicks the CTA or close button.
  const [blocked, setBlocked] = useState<{ id: string; title: string } | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const blockedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleBlocked = useCallback((game: SharedGame) => {
    setBlocked({ id: game.id, title: game.title });
  }, []);

  const handleDismissBlocked = useCallback(() => {
    setBlocked(null);
  }, []);

  const handleNavigateBlocked = useCallback(() => {
    if (!blocked || !onNavigateToGame) return;
    onNavigateToGame(blocked.id);
    setBlocked(null);
  }, [blocked, onNavigateToGame]);

  // Auto-dismiss timer — runs whenever a new blocked alert appears.
  useEffect(() => {
    if (!blocked) return;
    blockedTimerRef.current = setTimeout(() => {
      setBlocked(null);
    }, BLOCKED_AUTO_DISMISS_MS);
    return () => {
      if (blockedTimerRef.current) clearTimeout(blockedTimerRef.current);
    };
  }, [blocked]);

  // Debounce search input
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setDebouncedSearch(rawSearch.trim());
      setPage(1);
    }, DEBOUNCE_MS);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [rawSearch]);

  const { data, isLoading } = useSharedGames({
    searchTerm: debouncedSearch || undefined,
    page,
    pageSize: PAGE_SIZE,
    status: 2, // Published / Active
  });

  // #2275 — Batched library-status lookup for all cards on the current
  // page. Replaces the per-card useGameInLibraryStatus(gameId) that was
  // firing N (≤9) HTTP requests per fresh search. The hook returns a
  // results map keyed by gameId; cards read their own status from it.
  const gameIds = data?.items.map(g => g.id) ?? [];
  const { data: batchStatus } = useBatchGameStatus(gameIds);

  const addMutation = useAddGameToLibrary();

  const handleSelect = useCallback(
    async (gameId: string, gameName: string) => {
      setSelectingId(gameId);
      try {
        await addMutation.mutateAsync({ gameId });
        toast.success(t('pages.library.addGame.catalog.toastAdded', { gameName }));
        onSelect(gameId);
      } catch {
        toast.error(t('pages.library.addGame.catalog.toastError', { gameName }));
      } finally {
        setSelectingId(null);
      }
    },
    [addMutation, onSelect, t]
  );

  const totalPages = data ? Math.ceil(data.total / PAGE_SIZE) : 0;

  const cardLabels = {
    noImage: t('pages.library.addGame.catalog.noImage'),
    inLibraryBadge: t('pages.library.addGame.catalog.inLibraryBadge'),
    alreadyInLibrary: t('pages.library.addGame.catalog.alreadyInLibrary'),
    selectCta: t('pages.library.addGame.catalog.selectCta'),
  };

  return (
    <div className="flex flex-col gap-4 px-4 py-4" data-testid="catalog-search-step">
      {/* Back + search row */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          onClick={onBack}
          aria-label={t('pages.library.addGame.catalog.backAriaLabel')}
          data-testid="catalog-search-back"
        >
          {/* F2.2 T6 #1974 a11y audit: decorative icon — the button already
              carries its accessible name via aria-label. */}
          <ArrowLeft aria-hidden="true" className="h-4 w-4" />
        </Button>

        <div className="relative flex-1">
          {/* F2.2 T6 #1974 a11y audit: decorative search glyph — the input
              already carries its accessible name via aria-label + the
              visible placeholder. */}
          <Search
            aria-hidden="true"
            className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground"
          />
          <input
            type="search"
            placeholder={t('pages.library.addGame.catalog.searchPlaceholder')}
            value={rawSearch}
            onChange={e => setRawSearch(e.target.value)}
            className={[
              'w-full pl-9 pr-3 py-2 text-sm rounded-md',
              'border border-input bg-background',
              'focus:outline-none focus:ring-2 focus:ring-ring',
            ].join(' ')}
            data-testid="catalog-search-input"
            aria-label={t('pages.library.addGame.catalog.searchAriaLabel')}
          />
        </div>
      </div>

      {/* Results count */}
      {data && (
        <p className="text-xs text-muted-foreground" data-testid="catalog-search-count">
          {t('pages.library.addGame.catalog.resultCount', { count: data.total })}
        </p>
      )}

      {/* #2269 P0-2 (M2) — blocked-alert: surfaces when a user clicks the
          surface of an already-in-library card. Non-intrusive (role=status),
          auto-dismiss after 4.5s, CTA jumps to the game's detail page. */}
      {blocked && onNavigateToGame && (
        <div
          data-testid="catalog-blocked-alert"
          role="status"
          className="flex items-center gap-3 rounded-md border border-border bg-muted/30 px-3 py-2 text-sm"
        >
          <span className="flex-1 text-foreground">
            {t('pages.library.addGame.catalog.blockedMessage', { gameTitle: blocked.title })}
          </span>
          <Button
            variant="default"
            size="sm"
            onClick={handleNavigateBlocked}
            data-testid="catalog-blocked-cta"
          >
            {t('pages.library.addGame.catalog.blockedCta')}
          </Button>
          <Button
            variant="ghost"
            size="icon"
            onClick={handleDismissBlocked}
            aria-label={t('pages.library.addGame.catalog.blockedDismiss')}
            data-testid="catalog-blocked-dismiss"
            className="h-8 w-8"
          >
            <X aria-hidden="true" className="h-4 w-4" />
          </Button>
        </div>
      )}

      {/* Grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3" data-testid="catalog-search-grid">
        {isLoading
          ? Array.from({ length: PAGE_SIZE }).map((_, i) => <CardSkeleton key={i} />)
          : data?.items.map(game => (
              <SelectableGameCard
                key={game.id}
                game={game}
                onSelect={handleSelect}
                isSelecting={selectingId === game.id}
                inLibrary={batchStatus?.results[game.id]?.inLibrary ?? false}
                labels={cardLabels}
                onBlocked={onNavigateToGame ? handleBlocked : undefined}
              />
            ))}

        {!isLoading && data?.items.length === 0 && (
          <CatalogSearchEmptyState
            heading={t('pages.library.addGame.catalog.emptyResultsHeading')}
            subtitle={t('pages.library.addGame.catalog.emptyResultsSubtitle')}
            emptyTemplate={t('pages.library.addGame.catalog.emptyResults', {
              search: debouncedSearch,
            })}
            manualCtaLabel={t('pages.library.addGame.catalog.emptyManualCta')}
            onGoToManual={onGoToManual}
          />
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <CatalogPagination
          currentPage={page}
          totalPages={totalPages}
          onPageChange={setPage}
          totalResults={data?.total}
        />
      )}
    </div>
  );
}
