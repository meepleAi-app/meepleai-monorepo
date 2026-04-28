/**
 * Tabs — accessible tablist for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603) — absorbs Issue #588 (CategoryTabs Arrow-key a11y).
 * Implements WAI-ARIA tablist pattern with roving tabindex per spec §3.4.
 *
 * IMPORTANT spec deviation from mockup: spec mandates 5 tabs (overview / toolkits /
 * agents / knowledge / community); mockup `sp3-shared-game-detail.jsx` shows only
 * 3 tabs (toolkits / agents / kbs). The spec wins because:
 *  1. Closes #588 a11y requirements (full keyboard nav across stable taxonomy).
 *  2. Aligns with backend `SharedGameDetailDto` shape (Toolkits/Agents/Kbs +
 *     ContributorsCount + IsTopRated/IsNew flags exposed).
 *
 * Keyboard contract:
 *  - ArrowLeft  → previous tab (wraps to last)
 *  - ArrowRight → next tab (wraps to first)
 *  - Home       → first tab
 *  - End        → last tab
 *  - Tab        → exits tablist (focus moves to active tabpanel)
 *  - Enter/Space (browser default) → activates focused tab via onClick
 *
 * Activation is "automatic" (focus = activation) which mirrors the simpler
 * pattern recommended by WAI-ARIA APG when all tabpanels are statically rendered
 * (we toggle visibility via `hidden` + `aria-hidden`, not lazy mount).
 */

'use client';

import type { JSX, KeyboardEvent } from 'react';
import { useCallback, useRef } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

export const TAB_KEYS = ['overview', 'toolkits', 'agents', 'knowledge', 'community'] as const;

export type TabKey = (typeof TAB_KEYS)[number];

export interface TabDescriptor {
  readonly key: TabKey;
  readonly label: string;
  /** Optional count rendered in a pill next to the label. */
  readonly count?: number;
  /** Optional emoji rendered before the label. */
  readonly icon?: string;
}

export interface TabsLabels {
  /** Aria label for the tablist (e.g. "Game sections"). */
  readonly tablistAriaLabel: string;
}

export interface TabsProps {
  readonly tabs: ReadonlyArray<TabDescriptor>;
  readonly activeTab: TabKey;
  readonly onChange: (tab: TabKey) => void;
  readonly labels: TabsLabels;
  readonly className?: string;
  /** Passed-in tabpanel id base, e.g. "sg-detail" → tabpanel-sg-detail-overview. */
  readonly idBase?: string;
}

const DEFAULT_ID_BASE = 'sg-detail';

export function tabPanelId(idBase: string, key: TabKey): string {
  return `tabpanel-${idBase}-${key}`;
}

export function tabId(idBase: string, key: TabKey): string {
  return `tab-${idBase}-${key}`;
}

export function Tabs({
  tabs,
  activeTab,
  onChange,
  labels,
  className,
  idBase = DEFAULT_ID_BASE,
}: TabsProps): JSX.Element {
  const tabRefs = useRef<Map<TabKey, HTMLButtonElement>>(new Map());

  const focusTab = useCallback((key: TabKey) => {
    tabRefs.current.get(key)?.focus();
  }, []);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLButtonElement>, currentKey: TabKey) => {
      const orderedKeys = tabs.map(t => t.key);
      const idx = orderedKeys.indexOf(currentKey);
      if (idx === -1) return;

      let nextIdx: number | null = null;
      switch (e.key) {
        case 'ArrowLeft':
          nextIdx = (idx - 1 + orderedKeys.length) % orderedKeys.length;
          break;
        case 'ArrowRight':
          nextIdx = (idx + 1) % orderedKeys.length;
          break;
        case 'Home':
          nextIdx = 0;
          break;
        case 'End':
          nextIdx = orderedKeys.length - 1;
          break;
        default:
          return;
      }

      e.preventDefault();
      const nextKey = orderedKeys[nextIdx];
      onChange(nextKey);
      focusTab(nextKey);
    },
    [tabs, onChange, focusTab]
  );

  return (
    <div
      role="tablist"
      aria-label={labels.tablistAriaLabel}
      data-slot="shared-game-detail-tabs"
      className={clsx('flex flex-wrap items-center gap-1 border-b border-border pb-px', className)}
    >
      {tabs.map(tab => {
        const isActive = tab.key === activeTab;
        return (
          <button
            key={tab.key}
            ref={node => {
              if (node) {
                tabRefs.current.set(tab.key, node);
              } else {
                tabRefs.current.delete(tab.key);
              }
            }}
            type="button"
            role="tab"
            id={tabId(idBase, tab.key)}
            aria-selected={isActive}
            aria-controls={tabPanelId(idBase, tab.key)}
            tabIndex={isActive ? 0 : -1}
            data-tab-key={tab.key}
            data-active={isActive ? 'true' : 'false'}
            onClick={() => onChange(tab.key)}
            onKeyDown={e => handleKeyDown(e, tab.key)}
            className={clsx(
              'relative inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em]',
              'transition-[color,background-color] duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
              isActive ? 'text-foreground' : 'text-[hsl(var(--text-muted))] hover:text-foreground'
            )}
            style={isActive ? { backgroundColor: entityHsl('game', 0.12) } : undefined}
          >
            {tab.icon ? <span aria-hidden="true">{tab.icon}</span> : null}
            <span>{tab.label}</span>
            {tab.count != null ? (
              <span
                aria-hidden="true"
                data-dynamic="number"
                className={clsx(
                  'inline-flex min-w-[20px] items-center justify-center rounded-full px-1.5 py-0.5 text-[10px] font-bold tabular-nums',
                  isActive ? 'text-white' : 'bg-muted text-foreground'
                )}
                style={isActive ? { backgroundColor: entityHsl('game') } : undefined}
              >
                {tab.count}
              </span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
