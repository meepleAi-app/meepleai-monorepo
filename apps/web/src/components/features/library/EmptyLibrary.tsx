/**
 * EmptyLibrary — Wave B.3 v2 component (Issue #574),
 * SP4 first-run reskin (#1585 PR2 Task 2.5).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx:1029-1166`
 * (first-run + no-results + loading + error).
 * Plan: docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md
 *
 * Pure component (mirror Wave B.2 EmptyAgents): all i18n strings injected via
 * `labels`. Discriminates 4 surface states via `kind`:
 *   - 'loading'        → 8 skeleton cards desktop / 4 mobile (denser than B.2)
 *   - 'empty'          → SP4 first-run: 📚 illustration circle, primary "Aggiungi"
 *                        + secondary "Importa BGG" CTA, community suggestions box
 *                        with 3 (desktop) / 4 (mobile) static placeholder cards
 *   - 'filtered-empty' → 🔎 + onClearFilters CTA
 *   - 'error'          → ⚠️ + onRetry CTA
 *
 * NOTE: The "Suggerimenti dalla community" cards are static placeholders pending
 * the BGG hot-games API. See follow-up TODO below.
 */

import type { CSSProperties, ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type EmptyLibraryKind = 'empty' | 'filtered-empty' | 'error' | 'loading';

export interface EmptyLibraryCopy {
  readonly title: string;
  readonly subtitle: string;
  readonly cta: string;
}

export interface EmptyLibraryEmptyCopy extends EmptyLibraryCopy {
  /** Secondary CTA label used by the SP4 first-run state (mockup jsx:1059). */
  readonly ctaImportBgg?: string;
  /** Section header for the community suggestions box (mockup jsx:1068-1072). */
  readonly suggestions?: {
    readonly heading: string;
  };
}

export interface EmptyLibraryLabels {
  readonly empty: EmptyLibraryEmptyCopy;
  readonly filteredEmpty: EmptyLibraryCopy;
  readonly error: EmptyLibraryCopy;
}

export interface EmptyLibraryProps {
  readonly kind: EmptyLibraryKind;
  readonly labels: EmptyLibraryLabels;
  readonly onAddGame?: () => void;
  /** Secondary CTA for the SP4 first-run state (mockup jsx:1059-1064). */
  readonly onImportBgg?: () => void;
  readonly onClearFilters?: () => void;
  readonly onRetry?: () => void;
  readonly compact?: boolean;
  readonly className?: string;
}

const SKELETON_COUNT_DESKTOP = 8;
const SKELETON_COUNT_MOBILE = 4;

/**
 * Static placeholder community suggestions for the first-run state. Mirrors the
 * mockup `DS.games.slice(0, compact ? 4 : 3)` data (admin-mockups/design_files/
 * sp4-library-desktop.jsx:1078). Replace with the real BGG hot-games API once
 * a hub endpoint is exposed.
 *
 * TODO(#1585-followup): replace placeholders with `useHotGames()` / BE endpoint.
 */
type SuggestionPlaceholder = {
  readonly id: string;
  readonly title: string;
  readonly emoji: string;
  /** Tailwind gradient utility class for the icon tile background. */
  readonly tileClass: string;
  readonly rating: string;
};

const SUGGESTION_PLACEHOLDERS: readonly SuggestionPlaceholder[] = [
  {
    id: 'brass-birmingham',
    title: 'Brass: Birmingham',
    emoji: '🏭',
    tileClass: 'bg-gradient-to-br from-amber-700 to-amber-900',
    rating: '8.6',
  },
  {
    id: 'wingspan',
    title: 'Wingspan',
    emoji: '🦅',
    tileClass: 'bg-gradient-to-br from-emerald-500 to-teal-700',
    rating: '8.1',
  },
  {
    id: 'spirit-island',
    title: 'Spirit Island',
    emoji: '🌿',
    tileClass: 'bg-gradient-to-br from-lime-500 to-green-800',
    rating: '8.4',
  },
  {
    id: 'gloomhaven',
    title: 'Gloomhaven',
    emoji: '⚔️',
    tileClass: 'bg-gradient-to-br from-slate-600 to-slate-900',
    rating: '8.7',
  },
];

export function EmptyLibrary({
  kind,
  labels,
  onAddGame,
  onImportBgg,
  onClearFilters,
  onRetry,
  compact = false,
  className,
}: EmptyLibraryProps): ReactElement {
  if (kind === 'loading') {
    const count = compact ? SKELETON_COUNT_MOBILE : SKELETON_COUNT_DESKTOP;
    return (
      <div
        data-slot="library-empty-state"
        data-kind="loading"
        className={clsx(
          compact
            ? 'grid grid-cols-2 gap-3 px-4'
            : 'grid grid-cols-2 gap-4 px-4 sm:grid-cols-3 sm:px-6 lg:grid-cols-4',
          className
        )}
        aria-busy="true"
        aria-live="polite"
      >
        {Array.from({ length: count }, (_, index) => (
          <Skeleton
            key={index}
            data-slot="library-empty-skeleton"
            className="h-48 w-full rounded-2xl"
          />
        ))}
      </div>
    );
  }

  // ─── SP4 first-run state (mockup jsx:1029-1105) ──────────────────────────
  if (kind === 'empty') {
    const copy = labels.empty;
    const suggestionsCount = compact ? 4 : 3;
    const suggestions = SUGGESTION_PLACEHOLDERS.slice(0, suggestionsCount);
    const illustrationStyle: CSSProperties = {
      background: 'radial-gradient(circle, hsl(var(--c-game) / 0.18) 0%, transparent 70%)',
    };
    const primaryCtaStyle: CSSProperties = {
      boxShadow: '0 4px 14px hsl(var(--c-game) / 0.4)',
    };

    return (
      <div
        data-slot="library-empty-state"
        data-kind="empty"
        className={clsx(
          'flex flex-col items-center rounded-xl border border-dashed border-border-strong bg-card p-12 px-6 text-center',
          className
        )}
        role="status"
      >
        <div
          data-slot="empty-illustration"
          className="mb-3.5 flex h-24 w-24 items-center justify-center rounded-full text-[44px]"
          style={illustrationStyle}
          aria-hidden="true"
        >
          📚
        </div>
        <h2 className="mb-1.5 mt-0 font-display text-xl font-extrabold text-foreground">
          {copy.title}
        </h2>
        <p className="mb-[18px] mt-0 max-w-[380px] text-[13.5px] leading-[1.55] text-muted-foreground">
          {copy.subtitle}
        </p>
        <div className="mb-[22px] flex flex-wrap items-center justify-center gap-2">
          <button
            type="button"
            onClick={onAddGame}
            data-slot="library-empty-state-cta"
            style={primaryCtaStyle}
            className="cursor-pointer rounded-md border-0 bg-entity-game px-4 py-[9px] font-display text-[13px] font-extrabold text-white"
          >
            {copy.cta}
          </button>
          {copy.ctaImportBgg ? (
            <button
              type="button"
              onClick={onImportBgg}
              data-slot="library-empty-state-cta-import-bgg"
              className="cursor-pointer rounded-md border border-border-strong bg-background px-3.5 py-[9px] font-display text-[13px] font-bold text-foreground"
            >
              {copy.ctaImportBgg}
            </button>
          ) : null}
        </div>
        {copy.suggestions ? (
          <div data-slot="empty-suggestions" className="w-full max-w-[480px]">
            <div className="mb-2 text-left font-mono text-[10px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
              {copy.suggestions.heading}
            </div>
            <div className={clsx('grid gap-2', compact ? 'grid-cols-2' : 'grid-cols-3')}>
              {suggestions.map(sugg => (
                <div
                  key={sugg.id}
                  data-slot="empty-suggestion-card"
                  className="flex cursor-pointer items-center gap-2 rounded-md border border-border bg-background p-2.5"
                >
                  <div
                    className={clsx(
                      'flex h-8 w-8 shrink-0 items-center justify-center rounded-sm text-[16px]',
                      sugg.tileClass
                    )}
                    aria-hidden="true"
                  >
                    {sugg.emoji}
                  </div>
                  <div className="min-w-0 flex-1 text-left">
                    <div className="truncate font-display text-[11.5px] font-extrabold text-foreground">
                      {sugg.title}
                    </div>
                    <div className="font-mono text-[9px] font-bold text-muted-foreground">
                      ★ {sugg.rating}
                    </div>
                  </div>
                  <span aria-hidden="true" className="text-xs font-extrabold text-entity-game">
                    +
                  </span>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    );
  }

  // ─── Existing filtered-empty / error states (unchanged) ──────────────────
  const copy = kind === 'filtered-empty' ? labels.filteredEmpty : labels.error;
  const onCtaClick = kind === 'filtered-empty' ? onClearFilters : onRetry;
  const icon = kind === 'filtered-empty' ? '🔎' : '⚠️';

  return (
    <div
      data-slot="library-empty-state"
      data-kind={kind}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-12 text-center',
        compact ? 'mx-4' : 'mx-4 sm:mx-8',
        className
      )}
      role={kind === 'error' ? 'alert' : 'status'}
    >
      <span className="text-4xl" aria-hidden="true">
        {icon}
      </span>
      <h2 className="text-lg font-semibold text-foreground sm:text-xl">{copy.title}</h2>
      <p className="max-w-md text-sm text-muted-foreground">{copy.subtitle}</p>
      <button
        type="button"
        onClick={onCtaClick}
        className={clsx(
          'mt-2 inline-flex h-10 items-center justify-center rounded-full px-5 text-sm font-medium',
          'bg-primary text-primary-foreground shadow-sm transition-colors',
          'hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
        data-slot="library-empty-state-cta"
      >
        {copy.cta}
      </button>
    </div>
  );
}
