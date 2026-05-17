'use client';

import { useCallback, useEffect, useMemo, useRef } from 'react';

import { cn } from '@/lib/utils';

import { RowScroller } from './RowScroller';

import type { EntityFilter } from './EntityFilterPillBar';

/** Generic row card item — components only require an `id` and a `name`. */
export interface RowItemBase {
  readonly id: string;
  readonly name?: string;
  /** Display title for KB / generic cards. */
  readonly title?: string;
  /** Display label for top-contributor / player avatars. */
  readonly displayName?: string;
  readonly imageUrl?: string | null;
  readonly avatarUrl?: string | null;
  readonly coverImageUrl?: string | null;
  readonly publisher?: string | null;
  readonly year?: number | null;
  readonly gameName?: string | null;
  readonly authorName?: string | null;
  readonly installCount?: number;
  readonly invocationCount?: number;
  readonly chunkCount?: number;
  readonly docType?: string;
  readonly contributionCount?: number;
}

export type RowVariant = 'featured' | 'compact' | 'grid' | 'list-row';

export type RowState = 'enabled' | 'disabled';

export interface HorizontalRowProps {
  /** Stable identifier for telemetry + filter matching (e.g. "games", "agents"). */
  readonly rowId: EntityFilter | 'trending';
  /** Localised row title. */
  readonly title: string;
  /** Localised row subtitle (optional). */
  readonly subtitle?: string;
  /** Card layout variant. */
  readonly variant: RowVariant;
  /** Row data. Empty array shows the empty state. */
  readonly items: ReadonlyArray<RowItemBase>;
  /** Disabled-shell when backend endpoint is pending (#728). */
  readonly state?: RowState;
  /** Tooltip for the disabled-shell variant. */
  readonly disabledTooltip?: string;
  /** TanStack Query loading flag. */
  readonly isLoading?: boolean;
  /** TanStack Query error flag. */
  readonly isError?: boolean;
  /** Called when the row enters the viewport (used for telemetry on disabled rows). */
  readonly onVisible?: (rowId: HorizontalRowProps['rowId']) => void;
  /** Called when a card is clicked. */
  readonly onCardClick?: (rowId: HorizontalRowProps['rowId'], item: RowItemBase) => void;
  /** Localised label for the "show all" / "view all" link. */
  readonly viewAllLabel?: string;
  /** Localised retry button text (error state). */
  readonly retryLabel?: string;
  /** Localised empty state text. */
  readonly emptyLabel?: string;
  /** Called when retry button is clicked (error state). */
  readonly onRetry?: () => void;
  /** Optional className on outer section. */
  readonly className?: string;
  /** When false, the row is hidden via display:none (entity-filter mismatch). */
  readonly visible?: boolean;
}

const SKELETON_COUNT = 3;

/**
 * HorizontalRow — generic Netflix-style row with 4 card variants.
 *
 * Composes `RowScroller` + per-variant card layout. Supports loading / error /
 * empty / disabled-shell states. Telemetry hooks are external (`onVisible`,
 * `onCardClick`) so the component remains pure.
 */
export function HorizontalRow({
  rowId,
  title,
  subtitle,
  variant,
  items,
  state = 'enabled',
  disabledTooltip,
  isLoading = false,
  isError = false,
  onVisible,
  onCardClick,
  viewAllLabel,
  retryLabel = 'Riprova',
  emptyLabel = 'Nessun elemento disponibile.',
  onRetry,
  className,
  visible = true,
}: HorizontalRowProps) {
  const sectionRef = useRef<HTMLElement | null>(null);
  const visibleFiredRef = useRef(false);

  // IntersectionObserver: fire onVisible once per mount when row enters viewport.
  useEffect(() => {
    const el = sectionRef.current;
    if (!el || !onVisible || visibleFiredRef.current) return;
    if (typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(
      entries => {
        for (const entry of entries) {
          if (entry.isIntersecting && !visibleFiredRef.current) {
            visibleFiredRef.current = true;
            onVisible(rowId);
            observer.disconnect();
            return;
          }
        }
      },
      { threshold: 0.25 }
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [onVisible, rowId]);

  const isDisabled = state === 'disabled';
  const hasItems = items.length > 0;

  // Per-variant card sizing for keyboard scroll step
  const cardWidth = useMemo(() => {
    switch (variant) {
      case 'featured':
        return 320;
      case 'compact':
        return 260;
      case 'grid':
        return 180;
      case 'list-row':
        return 380;
    }
  }, [variant]);

  const renderCard = useCallback(
    (item: RowItemBase) => {
      const handleClick = () => onCardClick?.(rowId, item);
      const baseClass =
        'group/card relative shrink-0 snap-start overflow-hidden rounded-xl border border-border bg-card text-left transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-foreground/25';

      // 4 variant layouts
      if (variant === 'featured') {
        return (
          <button
            key={item.id}
            type="button"
            onClick={handleClick}
            data-slot="row-card"
            data-variant="featured"
            className={cn(baseClass, 'flex w-[320px] flex-col')}
            style={{ scrollSnapAlign: 'start' }}
          >
            <div
              className="flex h-[180px] items-center justify-center bg-gradient-to-br from-muted to-muted/40 text-4xl"
              aria-hidden="true"
            >
              🎲
            </div>
            <div className="flex flex-col gap-1 p-3">
              <div className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
                {item.name ?? item.title ?? 'Senza titolo'}
              </div>
              <div className="font-mono text-[10px] text-muted-foreground">
                {[item.publisher, item.year].filter(Boolean).join(' · ') || ' '}
              </div>
            </div>
          </button>
        );
      }

      if (variant === 'compact') {
        return (
          <button
            key={item.id}
            type="button"
            onClick={handleClick}
            data-slot="row-card"
            data-variant="compact"
            className={cn(baseClass, 'flex h-[72px] w-[260px] items-center gap-3 p-2')}
            style={{ scrollSnapAlign: 'start' }}
          >
            <div
              className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-muted text-xl"
              aria-hidden="true"
            >
              🤖
            </div>
            <div className="min-w-0 flex-1">
              <div className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
                {item.name ?? 'Senza nome'}
              </div>
              <div className="line-clamp-1 font-mono text-[10px] text-muted-foreground">
                {[item.gameName, item.invocationCount ? `${item.invocationCount} chiamate` : null]
                  .filter(Boolean)
                  .join(' · ') || ' '}
              </div>
            </div>
          </button>
        );
      }

      if (variant === 'grid') {
        return (
          <button
            key={item.id}
            type="button"
            onClick={handleClick}
            data-slot="row-card"
            data-variant="grid"
            className={cn(baseClass, 'flex h-[180px] w-[180px] flex-col')}
            style={{ scrollSnapAlign: 'start' }}
          >
            <div
              className="flex h-[110px] items-center justify-center bg-gradient-to-br from-muted to-muted/40 text-3xl"
              aria-hidden="true"
            >
              🧰
            </div>
            <div className="flex flex-1 flex-col gap-0.5 p-2">
              <div className="line-clamp-1 font-bold font-[Quicksand] text-xs text-foreground">
                {item.name ?? 'Senza nome'}
              </div>
              <div className="line-clamp-1 font-mono text-[9px] text-muted-foreground">
                {item.authorName ?? ' '}
              </div>
            </div>
          </button>
        );
      }

      // list-row variant
      return (
        <button
          key={item.id}
          type="button"
          onClick={handleClick}
          data-slot="row-card"
          data-variant="list-row"
          className={cn(baseClass, 'flex h-[80px] w-[380px] items-center gap-3 p-3')}
          style={{ scrollSnapAlign: 'start' }}
        >
          <div
            className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-muted text-xl"
            aria-hidden="true"
          >
            {item.avatarUrl ? '👤' : '📄'}
          </div>
          <div className="min-w-0 flex-1">
            <div className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
              {item.displayName ?? item.title ?? item.name ?? 'Senza titolo'}
            </div>
            <div className="line-clamp-1 font-mono text-[10px] text-muted-foreground">
              {[
                item.gameName,
                item.docType,
                item.chunkCount ? `${item.chunkCount} chunks` : null,
                item.contributionCount ? `${item.contributionCount} contributi` : null,
              ]
                .filter(Boolean)
                .join(' · ') || ' '}
            </div>
          </div>
        </button>
      );
    },
    [variant, onCardClick, rowId]
  );

  return (
    <section
      ref={sectionRef}
      data-slot="horizontal-row"
      data-row-id={rowId}
      data-state={state}
      aria-hidden={!visible}
      className={cn('flex flex-col gap-2 px-4 py-4 sm:px-8', !visible && 'hidden', className)}
    >
      <header className="flex items-baseline justify-between gap-3">
        <div className="flex flex-col">
          <h2 className="font-bold font-[Quicksand] text-lg text-foreground">
            {title}
            {isDisabled && (
              <span
                className="ml-2 inline-flex items-center rounded-full bg-muted px-2 py-0.5 align-middle font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground"
                title={disabledTooltip}
              >
                {disabledTooltip ?? 'Disponibile in Phase 0.5'}
              </span>
            )}
          </h2>
          {subtitle && <p className="text-xs text-muted-foreground">{subtitle}</p>}
        </div>
        {viewAllLabel && !isDisabled && hasItems && (
          <button
            type="button"
            className="text-xs font-bold font-[Quicksand] text-muted-foreground hover:text-foreground"
          >
            {viewAllLabel} →
          </button>
        )}
      </header>

      {/* Error state */}
      {isError && !isDisabled && (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-xl border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          <span>Errore di caricamento</span>
          {onRetry && (
            <button
              type="button"
              onClick={onRetry}
              className="font-bold font-[Quicksand] text-xs underline-offset-2 hover:underline"
            >
              {retryLabel}
            </button>
          )}
        </div>
      )}

      {/* Loading / disabled-shell / empty / cards */}
      {(isLoading || isDisabled) && (
        <RowScroller cardWidth={cardWidth} ariaLabel={`${title} (caricamento)`}>
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            <div
              key={i}
              data-slot="row-card-skeleton"
              className={cn(
                'shrink-0 animate-pulse rounded-xl border border-border bg-muted/40',
                variant === 'featured' && 'h-[244px] w-[320px]',
                variant === 'compact' && 'h-[72px] w-[260px]',
                variant === 'grid' && 'h-[180px] w-[180px]',
                variant === 'list-row' && 'h-[80px] w-[380px]'
              )}
              style={{ scrollSnapAlign: 'start' }}
            />
          ))}
        </RowScroller>
      )}

      {!isLoading && !isError && !isDisabled && !hasItems && (
        <div
          data-slot="row-empty"
          className="rounded-xl border border-dashed border-border bg-muted/30 px-4 py-6 text-center text-sm text-muted-foreground"
        >
          {emptyLabel}
        </div>
      )}

      {!isLoading && !isError && !isDisabled && hasItems && (
        <RowScroller cardWidth={cardWidth} ariaLabel={title}>
          {items.map(renderCard)}
        </RowScroller>
      )}
    </section>
  );
}
