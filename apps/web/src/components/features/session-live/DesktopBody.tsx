/**
 * DesktopBody — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * 3-column desktop layout shell (lg+ breakpoint only).
 * Hidden on mobile (MobileBody takes over below lg).
 *
 * Columns:
 *   left:   280px fixed — TurnIndicator + PlayerRosterLive
 *   center: flex-1     — LiveScoringPanel + ActionLogTimeline
 *   right:  340px fixed — RightColumnTabs (Interactions sub-PR adds tab wiring)
 *                         Foundation: placeholder with basic content
 *
 * Gate C: layout primitive, not a MeepleCard pattern.
 */

import type { ReactElement, ReactNode } from 'react';

// ─── Props ────────────────────────────────────────────────────────────────────

export interface DesktopBodyProps {
  readonly leftSidebar: ReactNode;
  readonly centerColumn: ReactNode;
  readonly rightColumn?: ReactNode;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function DesktopBody({
  leftSidebar,
  centerColumn,
  rightColumn,
}: DesktopBodyProps): ReactElement {
  return (
    <div
      data-slot="desktop-body"
      className="hidden flex-1 overflow-hidden lg:flex"
      aria-hidden={undefined} // accessible — screen readers interact with content
    >
      {/* Left sidebar: 280px */}
      <aside
        className="w-[280px] shrink-0 overflow-y-auto border-r border-slate-700/60
          bg-[hsl(240,40%,10%)]"
      >
        {leftSidebar}
      </aside>

      {/* Center column: flex-1 */}
      <main className="flex flex-1 flex-col overflow-y-auto p-4">{centerColumn}</main>

      {/* Right column: 340px — Foundation placeholder */}
      <aside
        className="w-[340px] shrink-0 overflow-y-auto border-l border-slate-700/60
          bg-[hsl(240,40%,10%)]"
      >
        {rightColumn ?? (
          <div
            data-slot="desktop-right-column-placeholder"
            className="flex h-full items-center justify-center p-4"
          >
            {/* RightColumnTabs (tools/chat/notes) added in Interactions sub-PR */}
            <div className="text-center text-xs text-muted-foreground">Tools, Chat &amp; Notes</div>
          </div>
        )}
      </aside>
    </div>
  );
}
