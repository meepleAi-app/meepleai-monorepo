'use client';

/**
 * RightColumnTabs — Issue #2374 G1 3-col layout (Task T1, sess.46r).
 *
 * Desktop right-column tab switcher (Score / Turni / Widget / Note).
 * Uses useTablistKeyboardNav (Wave A.6 PR #623) for WAI-ARIA tablist contract.
 *
 * Tab keys: 'score' | 'turn' | 'widget' | 'notes' (polymorphic dispatcher
 * matching mockup `sp4-session-skeleton-live.jsx`).
 *
 * Migration history:
 *   - Issue #750 (Wave D.2): initial 3-tab keys 'tools' | 'chat' | 'notes'.
 *   - Issue #2374 (sess.46r): renamed to mockup canonical 'score' | 'turn' |
 *     'widget' | 'notes' per plan §3 D-2.
 *
 * A11y:
 *   - role="tablist" aria-orientation="horizontal"
 *   - Roving tabindex (tab focused → tabIndex=0, others → -1)
 *   - ArrowLeft/Right navigation via useTablistKeyboardNav
 *   - Home/End jump
 *   - role="tab" + aria-selected on each tab button
 *   - role="tabpanel" on content area (linked via aria-labelledby)
 *
 * Gate C: DIVERGES from MeepleCard — tablist orchestration primitive, not
 *   a card pattern.
 *
 * data-slot="right-column-tabs" — required by unit tests.
 */

import { type ReactElement, useId, useMemo } from 'react';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';

// ─── Tab types ────────────────────────────────────────────────────────────────

export type LiveTab = 'score' | 'turn' | 'widget' | 'notes';

const ORDERED_TABS: ReadonlyArray<LiveTab> = ['score', 'turn', 'widget', 'notes'];

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface RightColumnTabsLabels {
  readonly tabsAriaLabel: string;
  readonly tabScore: string;
  readonly tabTurn: string;
  readonly tabWidget: string;
  readonly tabNotes: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface RightColumnTabsProps {
  readonly activeTab: LiveTab;
  readonly onTabChange: (next: LiveTab) => void;
  readonly children: React.ReactNode;
  readonly labels: RightColumnTabsLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function RightColumnTabs({
  activeTab,
  onTabChange,
  children,
  labels,
  className,
}: RightColumnTabsProps): ReactElement {
  const panelId = useId();
  const tablistId = useId();

  const tabLabels: Record<LiveTab, string> = useMemo(
    () => ({
      score: labels.tabScore,
      turn: labels.tabTurn,
      widget: labels.tabWidget,
      notes: labels.tabNotes,
    }),
    [labels.tabScore, labels.tabTurn, labels.tabWidget, labels.tabNotes]
  );

  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<LiveTab>({
    orderedKeys: ORDERED_TABS,
    onChange: onTabChange,
    orientation: 'horizontal',
  });

  return (
    <div
      data-slot="right-column-tabs"
      data-active-tab={activeTab}
      className={`flex flex-col ${className ?? ''}`}
    >
      {/* Tab bar */}
      <div
        id={tablistId}
        role="tablist"
        aria-label={labels.tabsAriaLabel}
        aria-orientation="horizontal"
        className="flex shrink-0 border-b border-border/60"
      >
        {ORDERED_TABS.map(tab => {
          const isActive = activeTab === tab;
          const tabId = `${tablistId}-tab-${tab}`;
          return (
            <button
              key={tab}
              id={tabId}
              ref={node => {
                if (node) tabRefs.current.set(tab, node);
                else tabRefs.current.delete(tab);
              }}
              role="tab"
              aria-selected={isActive}
              aria-controls={`${panelId}-panel`}
              tabIndex={isActive ? 0 : -1}
              onClick={() => onTabChange(tab)}
              onKeyDown={e => handleKeyDown(e, tab)}
              className={`flex-1 px-3 py-2.5 text-sm font-medium transition-colors
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset
                focus-visible:ring-ring
                ${
                  isActive
                    ? 'border-b-2 border-border text-foreground'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
            >
              {tabLabels[tab]}
            </button>
          );
        })}
      </div>

      {/* Tab panel */}
      <div
        id={`${panelId}-panel`}
        role="tabpanel"
        aria-labelledby={`${tablistId}-tab-${activeTab}`}
        className="flex-1 overflow-y-auto p-4"
      >
        {children}
      </div>
    </div>
  );
}
