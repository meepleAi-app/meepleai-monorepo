/**
 * MobileBody — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Mobile layout with bottom-nav tab bar (< lg breakpoint only).
 * Hidden on desktop (DesktopBody takes over at lg+).
 *
 * Tabs:
 *   score  — LiveScoringPanel (default)
 *   log    — ActionLogTimeline
 *   tools  — SessionToolsRail (Interactions sub-PR)
 *   chat   — LiveAgentChat (Interactions sub-PR)
 *
 * A11y: role="tablist" + roving tabindex for bottom-nav tabs.
 *
 * Gate C: layout primitive, not a MeepleCard pattern.
 */

import type { ReactElement, ReactNode } from 'react';

// ─── Types ────────────────────────────────────────────────────────────────────

export type MobileTab = 'score' | 'log' | 'tools' | 'chat';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface MobileBodyLabels {
  readonly tabScore: string;
  readonly tabLog: string;
  readonly tabTools: string;
  readonly tabChat: string;
  readonly bottomNavAriaLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface MobileBodyProps {
  readonly activeTab: MobileTab;
  readonly onTabChange: (next: MobileTab) => void;
  readonly content: ReactNode;
  readonly labels: MobileBodyLabels;
}

// ─── Tab config ───────────────────────────────────────────────────────────────

const TABS: ReadonlyArray<{ id: MobileTab; labelKey: keyof MobileBodyLabels }> = [
  { id: 'score', labelKey: 'tabScore' },
  { id: 'log', labelKey: 'tabLog' },
  { id: 'tools', labelKey: 'tabTools' },
  { id: 'chat', labelKey: 'tabChat' },
];

// ─── Component ────────────────────────────────────────────────────────────────

export function MobileBody({
  activeTab,
  onTabChange,
  content,
  labels,
}: MobileBodyProps): ReactElement {
  return (
    <div data-slot="mobile-body" className="flex flex-1 flex-col overflow-hidden lg:hidden">
      {/* Content area */}
      <div className="flex-1 overflow-y-auto p-4">{content}</div>

      {/* Bottom navigation */}
      <nav
        aria-label={labels.bottomNavAriaLabel}
        className="shrink-0 border-t border-slate-700/60 bg-[hsl(240,40%,10%)]"
      >
        <div
          role="tablist"
          aria-label={labels.bottomNavAriaLabel}
          aria-orientation="horizontal"
          className="flex"
        >
          {TABS.map(tab => {
            const isActive = activeTab === tab.id;
            const label = labels[tab.labelKey] as string;
            return (
              <button
                key={tab.id}
                type="button"
                role="tab"
                data-slot="mobile-body-tab"
                data-tab={tab.id}
                aria-selected={isActive}
                tabIndex={isActive ? 0 : -1}
                onClick={() => onTabChange(tab.id)}
                className={[
                  'flex flex-1 flex-col items-center justify-center gap-0.5 py-2.5',
                  'text-xs font-medium transition-colors focus-visible:outline-none',
                  'focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[hsl(240,60%,70%)]',
                  isActive
                    ? 'text-[hsl(240,60%,70%)] border-t-2 border-[hsl(240,60%,70%)] -mt-0.5'
                    : 'text-slate-500 hover:text-slate-300 border-t-2 border-transparent -mt-0.5',
                ].join(' ')}
              >
                {label}
              </button>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
