/**
 * PlayerTabs — 4-tab tablist for /players/[id] Stage 3 cluster (Issue #1113).
 *
 * Route-private wrapper. Reuses useTablistKeyboardNav hook for WAI-ARIA APG
 * keyboard contract (ArrowLeft/Right/Home/End, roving tabindex). The canonical
 * `Tabs` primitive in ui/detail-layout cannot be used directly because its
 * `TabKey` union is locked to shared-games detail values.
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';

export const PLAYER_TAB_KEYS = ['sessions', 'games', 'toolkits', 'achievements'] as const;
export type PlayerTabKey = (typeof PLAYER_TAB_KEYS)[number];

export interface PlayerTabsLabels {
  readonly tablistAriaLabel: string;
  readonly sessions: string;
  readonly games: string;
  readonly toolkits: string;
  readonly achievements: string;
}

export interface PlayerTabsProps {
  readonly activeTab: PlayerTabKey;
  readonly onChange: (next: PlayerTabKey) => void;
  readonly counts: Record<PlayerTabKey, number>;
  readonly labels: PlayerTabsLabels;
  readonly className?: string;
}

const ID_BASE = 'player-detail';

export function PlayerTabs({
  activeTab,
  onChange,
  counts,
  labels,
  className,
}: PlayerTabsProps): JSX.Element {
  const orderedKeys = useMemo(() => [...PLAYER_TAB_KEYS], []);
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<PlayerTabKey>({
    orderedKeys,
    onChange,
  });

  const labelOf = (key: PlayerTabKey): string => labels[key];

  return (
    <div
      data-slot="player-detail-tabs"
      role="tablist"
      aria-label={labels.tablistAriaLabel}
      className={clsx('flex items-center gap-1 overflow-x-auto', className)}
    >
      {PLAYER_TAB_KEYS.map((key) => {
        const isActive = key === activeTab;
        const count = counts[key];
        return (
          <button
            key={key}
            ref={(el) => {
              if (el) {
                tabRefs.current.set(key, el);
              } else {
                tabRefs.current.delete(key);
              }
            }}
            type="button"
            role="tab"
            id={`tab-${ID_BASE}-${key}`}
            aria-selected={isActive}
            aria-controls={`tabpanel-${ID_BASE}-${key}`}
            tabIndex={isActive ? 0 : -1}
            onClick={() => onChange(key)}
            onKeyDown={(event) => handleKeyDown(event, key)}
            className={clsx(
              'flex items-center gap-2 whitespace-nowrap rounded-t-md border-b-2 px-3 py-2 text-sm font-semibold transition-colors',
              isActive
                ? 'border-primary text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
          >
            <span>{labelOf(key)}</span>
            {count > 0 && (
              <span className="tabular-nums text-xs font-mono text-muted-foreground">{count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}
