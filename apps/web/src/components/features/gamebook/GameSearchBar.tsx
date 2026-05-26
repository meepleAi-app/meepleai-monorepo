/**
 * GameSearchBar — SP6 Phase C.1.B v2 component (Issue #789).
 *
 * Search input + Catalog/BGG segmented tabs for /gamebook/upload Step 1.
 * Pure component (Wave D.3 pattern): orchestrator pre-resolves all i18n
 * labels (incl. ICU plural counts on tabs) and injects via `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (Step1_Default search-bar + tabs). Uses `useTablistKeyboardNav` (Wave A.6
 * Tier 1 hook PR #623) for the WAI-ARIA APG tablist contract:
 * ArrowLeft/Right wrap + Home/End jump.
 *
 * a11y:
 *   - `<input type="search" role="searchbox">` with `aria-label` (no visual
 *     label per mockup — placeholder + icon convey purpose).
 *   - `<div role="tablist" aria-label="Sorgente catalogo">` wraps tabs.
 *   - Tabs are `<button role="tab" aria-selected={...}>`. Activation is
 *     automatic (focus = activation per APG, fired by useTablistKeyboardNav).
 *   - `aria-busy="true"` on root when `isPending=true` (queued search).
 *
 * data-slot="game-search-bar" + per-tab `data-slot="game-search-tab"` with
 * `data-active` for E2E selectors.
 */

'use client';

import { useMemo, type ReactElement, useCallback } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { GameSearchTab } from '@/lib/gamebook-upload';

export interface GameSearchBarLabels {
  /** Search input placeholder (e.g. "Cerca un gioco…"). */
  readonly placeholder: string;
  /** Catalog tab label (e.g. "Catalogo"). */
  readonly tabsCatalog: string;
  /** BGG tab label (e.g. "BGG"). */
  readonly tabsBgg: string;
  /** Search input aria-label. */
  readonly searchAria: string;
}

export interface GameSearchBarProps {
  /** Current search query value. */
  readonly query: string;
  /** Fires synchronously on every input change. */
  readonly onQueryChange: (next: string) => void;
  /** Currently selected tab. */
  readonly activeTab: GameSearchTab;
  /** Fires when user changes tab (click or keyboard). */
  readonly onTabChange: (next: GameSearchTab) => void;
  /** When true, renders aria-busy on root for SR feedback. */
  readonly isPending?: boolean;
  readonly labels: GameSearchBarLabels;
  readonly className?: string;
  /**
   * When true, renders the BGG tab. Default false (admin-only flow per spec
   * docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-phase-2-design.md).
   */
  readonly showBggTab?: boolean;
}

const FULL_TAB_ORDER: ReadonlyArray<GameSearchTab> = ['catalog', 'bgg'];
const CATALOG_ONLY_TAB_ORDER: ReadonlyArray<GameSearchTab> = ['catalog'];

// game + kb entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)

export function GameSearchBar({
  query,
  onQueryChange,
  activeTab,
  onTabChange,
  isPending,
  labels,
  className,
  showBggTab = false,
}: GameSearchBarProps): ReactElement {
  const orderedKeys = useMemo<ReadonlyArray<GameSearchTab>>(
    () => (showBggTab ? FULL_TAB_ORDER : CATALOG_ONLY_TAB_ORDER),
    [showBggTab]
  );

  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<GameSearchTab>({
    orderedKeys,
    onChange: onTabChange,
  });

  const handleInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onQueryChange(e.target.value);
    },
    [onQueryChange]
  );

  const tabConfigs: ReadonlyArray<{
    readonly key: GameSearchTab;
    readonly label: string;
    readonly activeCls: string;
  }> = showBggTab
    ? [
        {
          key: 'catalog',
          label: labels.tabsCatalog,
          activeCls: 'border-entity-game text-entity-game-text',
        },
        {
          key: 'bgg',
          label: labels.tabsBgg,
          activeCls: 'border-entity-document text-entity-document',
        },
      ]
    : [
        {
          key: 'catalog',
          label: labels.tabsCatalog,
          activeCls: 'border-entity-game text-entity-game-text',
        },
      ];

  return (
    <div
      data-slot="game-search-bar"
      aria-busy={isPending ? 'true' : undefined}
      className={clsx('flex flex-col gap-3', className)}
    >
      {/* Search input */}
      <label
        data-slot="game-search-bar-input-wrap"
        className={clsx(
          'flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2',
          'transition-colors motion-reduce:transition-none',
          'focus-within:border-foreground/40 focus-within:ring-2 focus-within:ring-ring/30'
        )}
      >
        <span aria-hidden="true" className="text-base text-foreground">
          🔍
        </span>
        <input
          type="search"
          role="searchbox"
          value={query}
          onChange={handleInputChange}
          placeholder={labels.placeholder}
          aria-label={labels.searchAria}
          className="flex-1 bg-transparent text-sm text-foreground placeholder:text-muted-foreground focus:outline-none"
        />
      </label>

      {/* Tabs */}
      <div
        role="tablist"
        aria-label="Sorgente catalogo"
        className="flex items-center gap-1 border-b border-border"
      >
        {tabConfigs.map(({ key, label, activeCls }) => {
          const isActive = activeTab === key;
          return (
            <button
              key={key}
              ref={node => {
                if (node) tabRefs.current.set(key, node);
                else tabRefs.current.delete(key);
              }}
              type="button"
              role="tab"
              aria-selected={isActive}
              tabIndex={isActive ? 0 : -1}
              data-slot="game-search-tab"
              data-tab-key={key}
              data-active={isActive ? 'true' : 'false'}
              onClick={() => onTabChange(key)}
              onKeyDown={e => handleKeyDown(e, key)}
              className={clsx(
                '-mb-px inline-flex items-center gap-1.5 border-b-2 px-3 py-2 text-sm font-semibold',
                'transition-colors motion-reduce:transition-none',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                isActive ? activeCls : 'border-transparent text-foreground'
              )}
            >
              {label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
