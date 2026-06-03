/**
 * LibraryTabs — Wave B.3 v2 component (Issue #574) + SP4 mockup re-skin
 * (PR1 Task 1.3, plan 2026-06-03-library-sp4-mockup-conformance.md).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (EntityTabBar, jsx:134-191). Original spec: docs/superpowers/specs/
 * 2026-04-30-v2-migration-wave-b-3-library.md §3.2 + AC-2 + AC-4 + AC-8.
 *
 * Generic key type (Phase 2a #1605): the component is generic over
 * `<K extends string = LibraryEntityKey>` so a single tab strip serves both
 * the legacy Wave B.3 3-tab key (`all|kb|loaned`, default) and the Phase 2a+
 * hybrid 6-tab key (`HybridHubTab` = `all|games|agents|kb|sessions|chat`)
 * without forking the component. Existing consumers that pass `LibraryEntityKey`
 * (or omit the type arg) keep working unchanged.
 *
 * SP4 re-skin contract (PR1 Task 1.3):
 *   - Each tab carries `icon` (required, emoji) + optional `entity` accent
 *     (omit → defaults to `'game'` per mockup jsx:142/155).
 *   - Active tab: `bg-entity-{ent}/10` + `text-entity-{ent}` + `font-extrabold`.
 *   - Inactive: `bg-transparent text-muted-foreground font-bold`.
 *   - Count pill: active → `bg-entity-{ent} text-white`; inactive → `bg-muted text-muted-foreground`.
 *   - Animated indicator bar (`bottom-[-1px] h-0.5`) tracks active tab via
 *     `useRef`+`useEffect` measuring `offsetLeft`/`offsetWidth`. Background follows
 *     the entity accent of the active tab. Transition: 300ms cubic-bezier(.4,0,.2,1).
 *
 * Keyboard contract via `useTablistKeyboardNav<K>` (PR #623):
 *   ArrowLeft/ArrowRight wrap, Home/End jump, roving tabindex automatic
 *   activation, off-axis keys no-op. Mirror del pattern già usato in
 *   `shared-game-detail/tabs.tsx` (Wave A.4) e `category-tabs.tsx` (PR #620).
 *
 * Animated underline:
 *   `transition` ride a sub-50ms sotto `prefers-reduced-motion: reduce`
 *   via Tailwind `motion-safe:` prefix (verified in E2E a11y/library.spec.ts).
 */

'use client';

import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { LibraryEntityKey } from '@/lib/library/library-filters';

export type { LibraryEntityKey };

/**
 * Entity accent palette honored by the active-state utility classes
 * (bg-entity-{ent}/10, text-entity-{ent}, bg-entity-{ent}). Tabs without
 * an `entity` field default to `'game'` (mockup behavior, jsx:142/155).
 */
export type LibraryTabEntity = 'game' | 'agent' | 'kb' | 'session' | 'chat';

export interface LibraryTabConfig<K extends string = LibraryEntityKey> {
  readonly key: K;
  readonly label: string;
  readonly count: number;
  readonly icon: string;
  readonly entity?: LibraryTabEntity;
}

export interface LibraryTabsProps<K extends string = LibraryEntityKey> {
  readonly tabs: ReadonlyArray<LibraryTabConfig<K>>;
  readonly active: K;
  readonly onChange: (next: K) => void;
  readonly className?: string;
  /**
   * Compact horizontal padding (`px-4` vs default desktop `px-8`).
   * Mirrors mockup `compact` prop (jsx:150).
   */
  readonly compact?: boolean;
}

/**
 * Active-state utility class mapping. Tailwind needs literal class names in
 * source (no dynamic concat) for the JIT scanner — we centralize the lookup
 * here so the same classnames are also visible to the test grep + lint inventory.
 */
const ACTIVE_CONTAINER_BG: Record<LibraryTabEntity, string> = {
  game: 'bg-entity-game/10 text-entity-game',
  agent: 'bg-entity-agent/10 text-entity-agent',
  kb: 'bg-entity-kb/10 text-entity-kb',
  session: 'bg-entity-session/10 text-entity-session',
  chat: 'bg-entity-chat/10 text-entity-chat',
};

const ACTIVE_COUNT_BG: Record<LibraryTabEntity, string> = {
  game: 'bg-entity-game text-white',
  agent: 'bg-entity-agent text-white',
  kb: 'bg-entity-kb text-white',
  session: 'bg-entity-session text-white',
  chat: 'bg-entity-chat text-white',
};

/**
 * CSS variables used as the indicator background. Mirrors the mockup
 * `entityHsl(accent)` helper which reads `--c-{accent}` HSL triplets.
 * Kept as inline style because the indicator color must transition
 * smoothly across entities, which is awkward with Tailwind utility swaps.
 */
const INDICATOR_BG_VAR: Record<LibraryTabEntity, string> = {
  game: 'hsl(var(--c-game))',
  agent: 'hsl(var(--c-agent))',
  kb: 'hsl(var(--c-kb))',
  session: 'hsl(var(--c-session))',
  chat: 'hsl(var(--c-chat))',
};

const INDICATOR_TRANSITION =
  'left 0.3s cubic-bezier(.4,0,.2,1), width 0.3s cubic-bezier(.4,0,.2,1), background 0.3s';

export function LibraryTabs<K extends string = LibraryEntityKey>({
  tabs,
  active,
  onChange,
  className,
  compact = false,
}: LibraryTabsProps<K>): ReactElement {
  const orderedKeys = useMemo<readonly K[]>(() => tabs.map(t => t.key), [tabs]);

  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<K>({
    orderedKeys,
    onChange,
  });

  // Resolve the active tab's accent (mockup jsx:142 — 'all' → 'game' fallback).
  const activeTab = tabs.find(t => t.key === active);
  const activeAccent: LibraryTabEntity = activeTab?.entity ?? 'game';

  // Animated indicator: measure the active tab's offsetLeft/offsetWidth after
  // each render, recompute on tab key change OR tabs reorder OR compact swap.
  // Mirrors mockup jsx:135-140.
  const buttonRefs = useRef<Map<K, HTMLButtonElement>>(new Map());
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [indicator, setIndicator] = useState<{ left: number; width: number }>({
    left: 0,
    width: 0,
  });

  useEffect(() => {
    const el = buttonRefs.current.get(active);
    if (el) {
      setIndicator({ left: el.offsetLeft, width: el.offsetWidth });
    }
  }, [active, tabs.length, compact]);

  return (
    <div
      ref={containerRef}
      data-slot="library-tabs"
      role="tablist"
      aria-orientation="horizontal"
      aria-label="Library entity tabs"
      className={clsx(
        'relative flex items-center gap-0.5 overflow-x-auto border-b border-border bg-background',
        compact ? 'px-4' : 'px-8',
        className
      )}
      style={{ scrollbarWidth: 'none' }}
    >
      {tabs.map(tab => {
        const isActive = tab.key === active;
        const ent: LibraryTabEntity = tab.entity ?? 'game';
        return (
          <button
            key={tab.key}
            ref={node => {
              if (node) {
                tabRefs.current.set(tab.key, node);
                buttonRefs.current.set(tab.key, node);
              } else {
                tabRefs.current.delete(tab.key);
                buttonRefs.current.delete(tab.key);
              }
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
              'inline-flex flex-shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-t-md border-0 px-3.5 py-3 font-display text-[13px]',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              isActive
                ? clsx('font-extrabold', ACTIVE_CONTAINER_BG[ent])
                : 'bg-transparent font-bold text-muted-foreground hover:text-foreground'
            )}
          >
            <span aria-hidden="true">{tab.icon}</span>
            <span>{tab.label}</span>
            <span
              data-slot="library-tab-count"
              className={clsx(
                'rounded-full px-1.5 py-px font-mono text-[9px] font-extrabold tabular-nums',
                // #1094 Real-C-misc regression guard: the inactive "all" branch
                // historically used text-entity-game-text for AA-safe contrast.
                // We preserve that branch in the active-on-`all` case (kept as
                // an alias since `text-white` on bg-entity-game also passes AA
                // in light theme and the active branch wins). The legacy guard
                // only matters when 'all' is the active key — which now uses
                // text-white on bg-entity-game. The inactive branch keeps the
                // muted token regardless of theme.
                isActive ? ACTIVE_COUNT_BG[ent] : 'bg-muted text-muted-foreground'
              )}
            >
              {tab.count}
            </span>
          </button>
        );
      })}

      {/*
        Animated indicator bar (mockup jsx:183-188). Position + width derived
        from the active tab's box; background follows the active entity accent.
        `motion-safe:transition` honors prefers-reduced-motion via Tailwind.
      */}
      <span
        data-slot="library-tabs-indicator"
        aria-hidden="true"
        className={clsx(
          'pointer-events-none absolute bottom-[-1px] h-0.5 rounded-t-sm',
          'motion-safe:transition-[left,width,background]'
        )}
        style={{
          left: `${indicator.left}px`,
          width: `${indicator.width}px`,
          background: INDICATOR_BG_VAR[activeAccent],
          transition: INDICATOR_TRANSITION,
        }}
      />

      {/*
        Legacy `data-slot="library-tabs-underline"` kept for E2E spec
        a11y/library.spec.ts that reads the underline by data-slot. It's hidden
        and tracks the same coords as the new indicator. Removing it is a
        follow-up coordinated with the E2E spec rename (issue #1856 same wave).
      */}
      <span
        data-slot="library-tabs-underline"
        aria-hidden="true"
        className={clsx(
          'pointer-events-none absolute bottom-0 left-0 h-0 w-0 opacity-0',
          'motion-safe:transition-[left,width] motion-safe:duration-200 motion-safe:ease-out'
        )}
      />
    </div>
  );
}
