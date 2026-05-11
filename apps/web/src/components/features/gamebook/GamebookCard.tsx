/**
 * GamebookCard — SP6 Phase B Task 2 v2 component (Issue #788).
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-index.jsx`
 * (GamebookCard + StatusPill + PipStrip).
 *
 * Pure component: all i18n strings injected via `labels` (orchestrator
 * resolves ICU plurals via next-intl `t(key, { count })` upstream — Wave D.3
 * pure-component pattern).
 *
 * Gate C MeepleCard fit decision (DIVERGE — pre-resolved per spec-panel):
 *   The shared `MeepleCard` API (avatar/title/subtitle/rating/cover) cannot
 *   express:
 *     1. A status pill with three discrete variants (ready/indexing/error)
 *        where the ERROR variant is interactive (retry button). MeepleCard
 *        has a single `StatusBadge` slot with no built-in interactivity.
 *     2. Inline indexing progress bar + "{indexed}/{total}" page counter
 *        rendered between body and chips. MeepleCard has no progress slot.
 *     3. A 4-chip metadata strip (pages / chunks / qa / sessions) inline,
 *        each with its own entity-color hint. MeepleCard's `ConnectionChip`
 *        helpers expect exactly 3 chips with fixed entity slots.
 *   Building bespoke. The only shared affordance is the cover (gradient
 *   linear-gradient OR emoji fallback) which we inline via a 16:9 box.
 *
 * Layout per status:
 *   - ready    → Full body: cover + title + meta + status pill + chunks
 *                counter + 3-chip strip (pages / qa / sessions, count > 0).
 *                Click → onClick(gamebookId).
 *   - indexing → Cover with desaturated overlay + spinner pill + progress
 *                "{indexed} di {total} pagine" + LinearProgress bar. Click
 *                disabled (status pill provides feedback, no navigation).
 *   - error    → Cover + title + meta + ErrorPill (button, fires onRetry…
 *                actually click on the whole card fires onClick — retry is
 *                handled by orchestrator route). Error message inline.
 *
 * a11y:
 *   - Card root is a `<button>` only when status='ready' (other statuses
 *     are inert <article>). Indexing status announces progress via
 *     aria-busy on cover area.
 *   - Status pill uses semantic colors with WCAG AA SC 1.4.3 ≥ 4.5:1.
 *
 * data-slot="gamebook-card" with `data-status={status}` for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { GamebookCardData, GamebookStatus } from '@/lib/gamebook-index/schemas';

export interface GamebookCardLabels {
  /** Status pill label — ready (e.g. "Pronto"). */
  readonly statusReady: string;
  /** Status pill label — indexing (e.g. "Indicizzazione…"). */
  readonly statusIndexing: string;
  /** Status pill label — error (e.g. "Errore · Riprova"). */
  readonly statusError: string;
  /** Pre-resolved ICU plural "X pagine" label (orchestrator. */
  readonly pagesCount: string;
  /** Pre-resolved "X chunks" label. */
  readonly chunksCount: string;
  /** Pre-resolved "X domande" label. */
  readonly qaCount: string;
  /** Pre-resolved "X sessioni" label. */
  readonly sessionsCount: string;
  /** Pre-resolved "{indexed} di {total} pagine" indexing progress text. */
  readonly indexingProgress: string;
  /** Inline retry button label inside error pill (e.g. "Riprova"). */
  readonly errorRetry: string;
  /** Aria-label on outer button for ready cards. */
  readonly openGamebook: string;
}

export interface GamebookCardProps {
  readonly gamebook: GamebookCardData;
  /** Click handler — orchestrator navigates to /library/[gameId]/play. */
  readonly onClick: (gamebookId: string) => void;
  readonly labels: GamebookCardLabels;
  readonly className?: string;
}

// Entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)
// Status pill and pip colours are now Tailwind classes (see below)

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

interface StatusPillProps {
  readonly status: GamebookStatus;
  readonly indexedLabel: string;
  readonly readyLabel: string;
  readonly errorLabel: string;
}

function StatusPill({
  status,
  indexedLabel,
  readyLabel,
  errorLabel,
}: StatusPillProps): ReactElement {
  if (status === 'ready') {
    return (
      <span
        data-slot="gamebook-card-status"
        data-status="ready"
        className="inline-flex items-center gap-1.5 rounded-full bg-entity-toolkit/12 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-entity-toolkit"
      >
        <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-entity-toolkit" />
        {readyLabel}
      </span>
    );
  }
  if (status === 'indexing') {
    return (
      <span
        data-slot="gamebook-card-status"
        data-status="indexing"
        className="inline-flex items-center gap-1.5 rounded-full bg-entity-game/12 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-entity-game"
      >
        <span
          aria-hidden="true"
          className="h-2 w-2 animate-spin rounded-full border-[1.5px] border-current border-t-transparent motion-reduce:animate-none"
        />
        {indexedLabel}
      </span>
    );
  }
  // error
  return (
    <span
      data-slot="gamebook-card-status"
      data-status="error"
      className="inline-flex items-center gap-1.5 rounded-full bg-entity-event/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-entity-event"
    >
      <span aria-hidden="true">⚠️</span>
      {errorLabel}
    </span>
  );
}

interface PipStripProps {
  readonly chunks: number;
  readonly qaCount: number;
  readonly sessionsCount: number;
  readonly chunksLabel: string;
  readonly qaLabel: string;
  readonly sessionsLabel: string;
}

function PipStrip({
  chunks,
  qaCount,
  sessionsCount,
  chunksLabel,
  qaLabel,
  sessionsLabel,
}: PipStripProps): ReactElement | null {
  const pips = [
    chunks > 0
      ? {
          key: 'chunks',
          icon: '📄',
          label: chunksLabel,
          cls: 'bg-entity-document/12 text-entity-document',
        }
      : null,
    qaCount > 0
      ? {
          key: 'qa',
          icon: '💬',
          label: qaLabel,
          cls: 'bg-entity-chat/12 text-entity-chat',
        }
      : null,
    sessionsCount > 0
      ? {
          key: 'sessions',
          icon: '🎯',
          label: sessionsLabel,
          cls: 'bg-entity-session/12 text-entity-session',
        }
      : null,
  ].filter((p): p is NonNullable<typeof p> => p !== null);

  if (pips.length === 0) return null;

  return (
    <div data-slot="gamebook-card-pip-strip" className="flex flex-wrap gap-1.5">
      {pips.map(p => (
        <span
          key={p.key}
          data-slot={`gamebook-card-pip-${p.key}`}
          className={clsx(
            'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold tabular-nums',
            p.cls
          )}
        >
          <span aria-hidden="true">{p.icon}</span>
          <span>{p.label}</span>
        </span>
      ))}
    </div>
  );
}

interface CoverProps {
  readonly cover: string | null;
  readonly emoji: string | null;
  readonly pagesLabel: string;
  readonly status: GamebookStatus;
}

function Cover({ cover, emoji, pagesLabel, status }: CoverProps): ReactElement {
  // Default fallback gradient (game→agent warm tone for visual brand consistency).
  // TODO #807-followup: two-hue warm gradient (hue 28/38) — near game entity but distinct shade; keep inline
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: two-hue warm gradient (game+agent); template literal, CSS vars cannot carry multi-stop gradients
  const fallbackGradient = `linear-gradient(155deg, hsl(28, 80%, 38%) 0%, hsl(38, 92%, 60%) 100%)`;
  const background = cover ?? fallbackGradient;

  return (
    <div
      data-slot="gamebook-card-cover"
      aria-hidden="true"
      className={clsx(
        'relative flex aspect-[16/9] w-full items-center justify-center overflow-hidden',
        status === 'indexing' && 'opacity-80'
      )}
      style={{ background }}
    >
      <span className="text-5xl drop-shadow-md">{emoji ?? '📖'}</span>
      <span
        data-slot="gamebook-card-cover-pages"
        className="absolute right-2 top-2 rounded-md bg-black/40 px-2 py-0.5 font-mono text-[10px] font-bold tabular-nums text-white backdrop-blur-sm"
      >
        {pagesLabel}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function GamebookCard({
  gamebook,
  onClick,
  labels,
  className,
}: GamebookCardProps): ReactElement {
  const isReady = gamebook.status === 'ready';
  const isIndexing = gamebook.status === 'indexing';
  const isError = gamebook.status === 'error';

  const ariaLabel = isReady ? `${labels.openGamebook}: ${gamebook.title}` : undefined;

  const pagesCoverLabel = `${gamebook.pages}/${gamebook.totalPages}`;

  const handleClick = (): void => {
    if (isReady) onClick(gamebook.id);
  };

  // Indexing progress percent for the linear bar.
  const indexingPct =
    gamebook.totalPages > 0
      ? Math.min(100, Math.round((gamebook.pages / gamebook.totalPages) * 100))
      : 0;

  const cardClasses = clsx(
    'group relative flex flex-col overflow-hidden rounded-xl border border-border bg-card text-left',
    'transition-shadow motion-reduce:transition-none',
    isReady && 'cursor-pointer hover:shadow-md focus-visible:shadow-md',
    isReady && 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
    !isReady && 'cursor-default',
    className
  );

  const innerContent = (
    <>
      <Cover
        cover={gamebook.cover}
        emoji={gamebook.emoji}
        pagesLabel={pagesCoverLabel}
        status={gamebook.status}
      />

      {/* Body */}
      <div className="flex flex-1 flex-col gap-2 p-3 sm:p-4">
        {/* Title + meta */}
        <div>
          <h3
            className="line-clamp-2 text-sm font-bold leading-tight text-foreground"
            title={gamebook.title}
            data-slot="gamebook-card-title"
          >
            {gamebook.title}
          </h3>
          <p
            data-slot="gamebook-card-meta"
            className="mt-0.5 truncate font-mono text-[11px] text-slate-700"
          >
            {gamebook.publisher ?? '—'}
            {gamebook.year != null && (
              <>
                <span className="mx-1 opacity-50">·</span>
                <span className="tabular-nums">{gamebook.year}</span>
              </>
            )}
          </p>
        </div>

        {/* Status row */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <StatusPill
            status={gamebook.status}
            readyLabel={labels.statusReady}
            indexedLabel={labels.statusIndexing}
            errorLabel={labels.statusError}
          />
          {isReady && gamebook.chunks > 0 && (
            <span
              data-slot="gamebook-card-chunks-counter"
              className="font-mono text-[10px] uppercase tracking-wide text-slate-700"
            >
              {labels.chunksCount}
            </span>
          )}
        </div>

        {/* Indexing progress bar (only visible during indexing) */}
        {isIndexing && (
          <div className="flex flex-col gap-1" data-slot="gamebook-card-indexing-progress">
            <p className="font-mono text-[10px] tabular-nums text-slate-700">
              {labels.indexingProgress}
            </p>
            <div aria-hidden="true" className="h-1 overflow-hidden rounded-full bg-slate-200">
              <div
                className="h-full rounded-full transition-[width] duration-300 motion-reduce:transition-none"
                style={{ width: `${indexingPct}%`, backgroundColor: 'var(--color-entity-game)' }}
              />
            </div>
          </div>
        )}

        {/* Pip strip — only when ready */}
        {isReady && (
          <PipStrip
            chunks={gamebook.chunks}
            qaCount={gamebook.qaCount}
            sessionsCount={gamebook.sessionsCount}
            chunksLabel={labels.pagesCount}
            qaLabel={labels.qaCount}
            sessionsLabel={labels.sessionsCount}
          />
        )}

        {/* Error message + retry button */}
        {isError && gamebook.errorMsg && (
          <div
            data-slot="gamebook-card-error-msg"
            role="alert"
            className="flex flex-col gap-2 rounded-md px-2.5 py-2 text-[11px] bg-entity-event/10 text-entity-event"
          >
            <p>{gamebook.errorMsg}</p>
            <button
              type="button"
              onClick={() => onClick(gamebook.id)}
              data-slot="gamebook-card-retry-cta"
              className={clsx(
                'self-start rounded-full border px-3 py-0.5 text-[10px] font-bold uppercase tracking-wide',
                'transition-colors motion-reduce:transition-none',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                'hover:bg-white/40',
                'border-entity-event text-entity-event'
              )}
            >
              {labels.errorRetry}
            </button>
          </div>
        )}
      </div>
    </>
  );

  // Ready: render as button (clickable). Else: inert article.
  if (isReady) {
    return (
      <button
        type="button"
        onClick={handleClick}
        aria-label={ariaLabel}
        data-slot="gamebook-card"
        data-gamebook-id={gamebook.id}
        data-status={gamebook.status}
        className={cardClasses}
      >
        {innerContent}
      </button>
    );
  }

  return (
    <article
      data-slot="gamebook-card"
      data-gamebook-id={gamebook.id}
      data-status={gamebook.status}
      aria-busy={isIndexing ? 'true' : undefined}
      className={cardClasses}
    >
      {innerContent}
    </article>
  );
}
