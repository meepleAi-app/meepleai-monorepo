/**
 * LibraryTabs — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (EntityTabBar). Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md
 * §3.2 + AC-2 + AC-4 + AC-8.
 *
 * Scope ridotto B.3 — 3 tabs (`all` / `kb` / `loaned`):
 *   - `game` droppato YAGNI (alias di `all` con backend game-only).
 *   - `archived` rinominato `loaned` per allineamento col mapping
 *     `currentState='InPrestito'` (§3.3).
 *   - `agent`/`session`/`chat` deferred a Wave B.4+ (backend extension
 *     child issue tracking).
 *
 * Keyboard contract via `useTablistKeyboardNav<LibraryEntityKey>` (PR #623):
 *   ArrowLeft/ArrowRight wrap, Home/End jump, roving tabindex automatic
 *   activation, off-axis keys no-op. Mirror del pattern già usato in
 *   `shared-game-detail/tabs.tsx` (Wave A.4) e `category-tabs.tsx` (PR #620).
 *
 * Animated underline:
 *   `motion-safe:transition` collapse a sub-50ms sotto
 *   `prefers-reduced-motion: reduce` (verified in E2E a11y/library.spec.ts).
 */

'use client';

import { useMemo, type ReactElement } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';

export type LibraryEntityKey = 'all' | 'kb' | 'loaned';

export interface LibraryTabConfig {
  readonly key: LibraryEntityKey;
  readonly label: string;
  readonly count: number;
}

export interface LibraryTabsProps {
  readonly tabs: ReadonlyArray<LibraryTabConfig>;
  readonly active: LibraryEntityKey;
  readonly onChange: (next: LibraryEntityKey) => void;
  readonly className?: string;
}

export function LibraryTabs({ tabs, active, onChange, className }: LibraryTabsProps): ReactElement {
  const orderedKeys = useMemo<readonly LibraryEntityKey[]>(() => tabs.map(t => t.key), [tabs]);

  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<LibraryEntityKey>({
    orderedKeys,
    onChange,
  });

  const activeIdx = Math.max(0, orderedKeys.indexOf(active));
  const underlineWidthPct = tabs.length > 0 ? 100 / tabs.length : 0;
  const underlineLeftPct = activeIdx * underlineWidthPct;

  return (
    <div
      data-slot="library-tabs"
      role="tablist"
      aria-orientation="horizontal"
      aria-label="Library entity tabs"
      className={clsx('relative flex items-center gap-1 border-b border-border', className)}
    >
      {tabs.map(tab => {
        const isActive = tab.key === active;
        return (
          <button
            key={tab.key}
            ref={node => {
              if (node) tabRefs.current.set(tab.key, node);
              else tabRefs.current.delete(tab.key);
            }}
            type="button"
            role="tab"
            aria-selected={isActive}
            tabIndex={isActive ? 0 : -1}
            data-slot="library-tab"
            data-tab-key={tab.key}
            onClick={() => onChange(tab.key)}
            onKeyDown={e => handleKeyDown(e, tab.key)}
            className={clsx(
              'relative flex flex-1 items-center justify-center gap-2 px-4 py-3 text-sm font-medium',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              isActive ? 'text-foreground' : 'text-muted-foreground hover:text-foreground'
            )}
          >
            <span>{tab.label}</span>
            <span
              data-slot="library-tab-count"
              className={clsx(
                'inline-flex h-5 min-w-[1.25rem] items-center justify-center rounded-full px-1.5 text-xs tabular-nums',
                isActive ? 'bg-primary/10 text-primary' : 'bg-muted text-muted-foreground'
              )}
            >
              {tab.count}
            </span>
          </button>
        );
      })}

      <span
        data-slot="library-tabs-underline"
        aria-hidden="true"
        className={clsx(
          'pointer-events-none absolute bottom-0 left-0 h-0.5 bg-primary',
          'motion-safe:transition-[left,width] motion-safe:duration-200 motion-safe:ease-out'
        )}
        style={{
          width: `${underlineWidthPct}%`,
          left: `${underlineLeftPct}%`,
        }}
      />
    </div>
  );
}
