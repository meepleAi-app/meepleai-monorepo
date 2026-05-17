/**
 * ToolkitTabs — 6-tab tablist for `/toolkits/[id]` Stage 3 cluster (Issue #1145).
 *
 * Mirror of `PlayerTabs` (Issue #1113) with disabled-state support per spec
 * `docs/superpowers/specs/2026-05-14-stage3-toolkit-detail.md` Path C: 4 tabs
 * (agent / kb / versions / ratings) are rendered as disabled-shell pending
 * Phase-5 backend (#822, #819). Disabled tab click emits telemetry but does
 * NOT change `activeTab`.
 *
 * Canonical `Tabs` primitive cannot be used because its `TabKey` union is
 * locked to shared-games detail values.
 *
 * Keyboard contract: ArrowLeft/Right/Home/End across the **enabled** subset
 * only (roving tabindex skips disabled tabs).
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';

export const TOOLKIT_TAB_KEYS = [
  'overview',
  'agent',
  'kb',
  'tools',
  'versions',
  'ratings',
] as const;
export type ToolkitTabKey = (typeof TOOLKIT_TAB_KEYS)[number];

export interface ToolkitTabsLabels {
  readonly tablistAriaLabel: string;
  readonly overview: string;
  readonly agent: string;
  readonly kb: string;
  readonly tools: string;
  readonly versions: string;
  readonly ratings: string;
  /** Tooltip text on disabled tabs. */
  readonly disabledTooltip: string;
}

export interface ToolkitTabsProps {
  readonly activeTab: ToolkitTabKey;
  readonly onChange: (next: ToolkitTabKey) => void;
  /** Tabs in this set are rendered as `aria-disabled="true"` with tooltip. */
  readonly disabledTabs: ReadonlyArray<ToolkitTabKey>;
  /** Optional counts rendered as monospaced pill (kb count, tools count, etc.) */
  readonly counts?: Partial<Record<ToolkitTabKey, number>>;
  readonly labels: ToolkitTabsLabels;
  /** Called when a disabled tab is clicked — for telemetry. Tab does NOT change. */
  readonly onDisabledAttempt?: (key: ToolkitTabKey) => void;
  readonly className?: string;
}

const ID_BASE = 'toolkit-detail';

export function tabId(key: ToolkitTabKey): string {
  return `tab-${ID_BASE}-${key}`;
}

export function tabPanelId(key: ToolkitTabKey): string {
  return `tabpanel-${ID_BASE}-${key}`;
}

export function ToolkitTabs({
  activeTab,
  onChange,
  disabledTabs,
  counts,
  labels,
  onDisabledAttempt,
  className,
}: ToolkitTabsProps): JSX.Element {
  // Keyboard navigation only iterates the *enabled* subset
  const enabledKeys = useMemo(
    () => TOOLKIT_TAB_KEYS.filter(k => !disabledTabs.includes(k)),
    [disabledTabs]
  );
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<ToolkitTabKey>({
    orderedKeys: enabledKeys,
    onChange,
  });

  const labelOf = (key: ToolkitTabKey): string => labels[key];

  return (
    <div
      data-slot="toolkit-detail-tabs"
      role="tablist"
      aria-label={labels.tablistAriaLabel}
      className={clsx('flex items-center gap-1 overflow-x-auto', className)}
    >
      {TOOLKIT_TAB_KEYS.map(key => {
        const isActive = key === activeTab;
        const isDisabled = disabledTabs.includes(key);
        const count = counts?.[key];

        return (
          <button
            key={key}
            ref={el => {
              if (isDisabled) return; // disabled tabs not in roving tabindex
              if (el) {
                tabRefs.current.set(key, el);
              } else {
                tabRefs.current.delete(key);
              }
            }}
            type="button"
            role="tab"
            id={tabId(key)}
            data-tab-key={key}
            aria-selected={isActive}
            aria-controls={tabPanelId(key)}
            aria-disabled={isDisabled || undefined}
            title={isDisabled ? labels.disabledTooltip : undefined}
            tabIndex={isDisabled ? -1 : isActive ? 0 : -1}
            onClick={() => {
              if (isDisabled) {
                onDisabledAttempt?.(key);
                return;
              }
              onChange(key);
            }}
            onKeyDown={event => !isDisabled && handleKeyDown(event, key)}
            className={clsx(
              'flex items-center gap-2 whitespace-nowrap rounded-t-md border-b-2 px-3 py-2 text-sm font-semibold transition-colors',
              isDisabled
                ? 'cursor-not-allowed border-transparent text-muted-foreground/50'
                : isActive
                  ? 'border-primary text-foreground'
                  : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
          >
            <span>{labelOf(key)}</span>
            {count !== undefined && count > 0 && (
              <span className="tabular-nums text-xs font-mono text-muted-foreground">{count}</span>
            )}
            {isDisabled && (
              <span
                aria-hidden="true"
                className="font-mono text-[9px] uppercase tracking-wider text-muted-foreground/70"
              >
                P5
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
