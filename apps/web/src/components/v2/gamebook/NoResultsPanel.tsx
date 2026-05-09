/**
 * NoResultsPanel — SP6 Phase C.1.B v2 component (Issue #789).
 *
 * Empty-state for /gamebook/upload Step 1 when both Catalog AND BGG
 * searches return zero results for the user's query. Renders a 🔭 telescope
 * illustration + title (with query interpolated by orchestrator) + 3
 * `ActionCard` fallbacks: Create new game, Search BGG, Add private game.
 *
 * Pure component (Wave D.3 pattern). Title is pre-resolved with `{query}`
 * interpolation upstream; this component only places it.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (Step1_NoResults).
 *
 * a11y:
 *   - `<section role="region" aria-label="Risultati di ricerca vuoti">`
 *   - Illustration (`aria-hidden="true"`) is decorative emoji.
 *   - 3 ActionCards are full `<button>` semantics with `aria-label`s.
 *
 * data-slot="no-results-panel" + `data-query` for E2E.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { ActionCard } from './ActionCard';

export interface NoResultsPanelLabels {
  /** Pre-resolved title with `{query}` interpolated (e.g. `"Zaardam" non trovato`). */
  readonly title: string;
  /** Subtitle line (e.g. "Tre modi per continuare:"). */
  readonly description: string;
  /** Create-new action labels. */
  readonly actionCardCreate: {
    readonly title: string;
    readonly description: string;
  };
  /** Search-BGG action labels. */
  readonly actionCardBgg: {
    readonly title: string;
    readonly description: string;
  };
  /** Add-private action labels. */
  readonly actionCardPrivate: {
    readonly title: string;
    readonly description: string;
  };
}

export interface NoResultsPanelProps {
  /** User's search query (used for data attribute / debugging only). */
  readonly query: string;
  /** Click handler for "Crea gioco nuovo" action. */
  readonly onCreateNew: () => void;
  /** Click handler for "Cerca su BGG" action. */
  readonly onSearchBgg: () => void;
  /** Click handler for "Indicizza solo per me" action. */
  readonly onAddPrivate: () => void;
  readonly labels: NoResultsPanelLabels;
  readonly className?: string;
}

// game entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)

export function NoResultsPanel({
  query,
  onCreateNew,
  onSearchBgg,
  onAddPrivate,
  labels,
  className,
}: NoResultsPanelProps): ReactElement {
  return (
    <section
      data-slot="no-results-panel"
      data-query={query}
      role="region"
      aria-label="Risultati di ricerca vuoti"
      className={clsx('flex flex-col items-center gap-3 px-4 py-8 text-center sm:px-6', className)}
    >
      {/* Illustration */}
      <div
        data-slot="no-results-panel-illustration"
        aria-hidden="true"
        className="flex h-20 w-20 items-center justify-center rounded-2xl border border-dashed border-entity-game/40 bg-entity-game/12"
      >
        <span className="text-4xl">🔭</span>
      </div>

      {/* Title */}
      <h3
        data-slot="no-results-panel-title"
        className="text-base font-bold tracking-tight text-foreground sm:text-lg"
      >
        {labels.title}
      </h3>

      {/* Description */}
      <p
        data-slot="no-results-panel-description"
        className="max-w-md text-sm leading-relaxed text-slate-700"
      >
        {labels.description}
      </p>

      {/* 3 ActionCards */}
      <div
        data-slot="no-results-panel-actions"
        className="mt-2 flex w-full max-w-md flex-col gap-2"
      >
        <ActionCard
          icon={<span>＋</span>}
          title={labels.actionCardCreate.title}
          description={labels.actionCardCreate.description}
          onClick={onCreateNew}
        />
        <ActionCard
          icon={<span>🌐</span>}
          title={labels.actionCardBgg.title}
          description={labels.actionCardBgg.description}
          onClick={onSearchBgg}
        />
        <ActionCard
          icon={<span>🔒</span>}
          title={labels.actionCardPrivate.title}
          description={labels.actionCardPrivate.description}
          onClick={onAddPrivate}
        />
      </div>
    </section>
  );
}
