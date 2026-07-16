'use client';

/**
 * MobileBody — Issue #2374 G1 (T9 refactor, sess.46r).
 *
 * Mobile layout (< lg breakpoint only). Hidden on desktop (DesktopBody takes
 * over at lg+). REFACTORED from the legacy 4-tab bottom-nav (Foundation #746)
 * to the mockup canonical bottom-sheet pattern (parts.jsx L266-286):
 *
 *   - Main area = full-width = ChatAgentPanel + ActionLogTimeline stacked
 *     (mirrors desktop LEFT 60%).
 *   - Floating action button (bottom-right) opens MobileBottomSheetDrawer.
 *   - Drawer hosts the 4 polymorphic tabs (Score/Turn/Widget/Notes) — same
 *     content as desktop RIGHT 40%.
 *
 * Tab union: shared `LiveTab` ('score'|'turn'|'widget'|'notes') from
 * RightColumnTabs. Legacy `MobileTab` ('score'|'log'|'tools'|'chat') is
 * DELETED — log is now always-visible in the main column, tools renamed to
 * widget, chat is the always-visible ChatAgentPanel.
 *
 * URL SSOT: parent owns `?msheet=open|closed` + `?mtab=` via the
 * sheetOpen/onSheetOpenChange + sheetActiveTab/onSheetTabChange props.
 *
 * Gate C: layout primitive, not a MeepleCard pattern.
 */

import type { ReactElement, ReactNode } from 'react';

import { MobileBottomSheetDrawer } from './MobileBottomSheetDrawer';

import type { LiveTab } from './RightColumnTabs';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface MobileBodyLabels {
  /** Floating action button label (mockup CTA "Apri pannello"). */
  readonly openSheetCta: string;
  /** aria-label for the drawer close button. */
  readonly closeSheetAriaLabel: string;
  /** Drawer sr-only title (Radix Dialog a11y contract). */
  readonly drawerTitle: string;
  /** aria-label for the drawer's role="tablist". */
  readonly tabsAriaLabel: string;
  readonly tabFlavor: string;
  readonly tabScore: string;
  readonly tabTurn: string;
  readonly tabWidget: string;
  readonly tabNotes: string;
  readonly tabPhotos: string;
  readonly tabAgent: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface MobileBodyProps {
  /** Full-width LEFT-equivalent content (ChatAgentPanel + ActionLogTimeline). */
  readonly mainContent: ReactNode;
  /** Drawer open state (URL SSOT in parent: ?msheet=open|closed). */
  readonly sheetOpen: boolean;
  readonly onSheetOpenChange: (open: boolean) => void;
  /** Active tab in the drawer (URL SSOT in parent: ?mtab=score|turn|widget|notes). */
  readonly sheetActiveTab: LiveTab;
  readonly onSheetTabChange: (next: LiveTab) => void;
  /** Tab body for the currently active drawer tab (parent owns the switch). */
  readonly sheetContent: ReactNode;
  readonly labels: MobileBodyLabels;
  /** #2787 — when true, the drawer shows a game-conditional 'flavor' tab. */
  readonly showFlavorTab?: boolean;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function MobileBody({
  mainContent,
  sheetOpen,
  onSheetOpenChange,
  sheetActiveTab,
  onSheetTabChange,
  sheetContent,
  labels,
  showFlavorTab = false,
}: MobileBodyProps): ReactElement {
  return (
    <div
      data-slot="mobile-body"
      data-layout="mobile-sheet"
      className="relative flex flex-1 flex-col overflow-hidden lg:hidden"
    >
      {/* Main area — full-width LEFT-equivalent (ChatAgentPanel + ActionLogTimeline) */}
      <main className="flex-1 overflow-y-auto p-3">{mainContent}</main>

      {/* Floating action button — opens bottom-sheet drawer */}
      <button
        type="button"
        data-slot="mobile-body-open-sheet"
        aria-label={labels.openSheetCta}
        onClick={() => onSheetOpenChange(true)}
        className="absolute bottom-4 right-4 z-30 flex h-12 w-12
          items-center justify-center rounded-full border
          border-entity-session bg-entity-session text-white shadow-lg
          transition-transform hover:scale-105 focus-visible:outline-none
          focus-visible:ring-2 focus-visible:ring-ring
          focus-visible:ring-offset-2 focus-visible:ring-offset-background"
      >
        {/* Three-dots vertical icon → indicates "more / panel" */}
        <svg
          xmlns="http://www.w3.org/2000/svg"
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <line x1="3" y1="12" x2="21" y2="12" />
          <line x1="3" y1="6" x2="21" y2="6" />
          <line x1="3" y1="18" x2="21" y2="18" />
        </svg>
      </button>

      {/* Bottom-sheet drawer — hosts the 4 polymorphic tabs */}
      <MobileBottomSheetDrawer
        open={sheetOpen}
        onOpenChange={onSheetOpenChange}
        activeTab={sheetActiveTab}
        onTabChange={onSheetTabChange}
        showFlavorTab={showFlavorTab}
        labels={{
          drawerTitle: labels.drawerTitle,
          closeAriaLabel: labels.closeSheetAriaLabel,
          tabsAriaLabel: labels.tabsAriaLabel,
          tabFlavor: labels.tabFlavor,
          tabScore: labels.tabScore,
          tabTurn: labels.tabTurn,
          tabWidget: labels.tabWidget,
          tabNotes: labels.tabNotes,
          tabPhotos: labels.tabPhotos,
          tabAgent: labels.tabAgent,
        }}
      >
        {sheetContent}
      </MobileBottomSheetDrawer>
    </div>
  );
}
