/**
 * LibraryTabs — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (EntityTabBar). Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md
 * §3.2 + AC-2 + AC-4 + AC-8.
 *
 * Generic key type (Phase 2a #1605): the component is generic over
 * `<K extends string = LibraryEntityKey>` so a single tab strip serves both
 * the legacy Wave B.3 3-tab key (`all|kb|loaned`, default) and the Phase 2a+
 * hybrid 6-tab key (`HybridHubTab` = `all|games|agents|kb|sessions|chat`)
 * without forking the component. Existing consumers that pass `LibraryEntityKey`
 * (or omit the type arg) keep working unchanged.
 *
 * Keyboard contract via `useTablistKeyboardNav<K>` (PR #623):
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
import type { LibraryEntityKey } from '@/lib/library/library-filters';

export type { LibraryEntityKey };

export interface LibraryTabConfig<K extends string = LibraryEntityKey> {
  readonly key: K;
  readonly label: string;
  readonly count: number;
}

export interface LibraryTabsProps<K extends string = LibraryEntityKey> {
  readonly tabs: ReadonlyArray<LibraryTabConfig<K>>;
  readonly active: K;
  readonly onChange: (next: K) => void;
  readonly className?: string;
}

export function LibraryTabs<K extends string = LibraryEntityKey>({
  tabs,
  active,
  onChange,
  className,
}: LibraryTabsProps<K>): ReactElement {
  const orderedKeys = useMemo<readonly K[]>(() => tabs.map(t => t.key), [tabs]);

  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<K>({
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
                isActive ? 'bg-primary/10 text-entity-game-text' : 'bg-muted text-muted-foreground'
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
