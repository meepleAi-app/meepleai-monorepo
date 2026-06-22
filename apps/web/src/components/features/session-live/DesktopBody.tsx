/**
 * DesktopBody — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Desktop layout shell (lg+ breakpoint only). Hidden on mobile.
 *
 * Two rendering paths:
 *
 *   1. NEW 2-zone 60/40 grid (G1 #2374). Triggered when `mainColumn` is
 *      provided. LEFT 60% = main column (ChatAgentPanel + ActionLogTimeline);
 *      RIGHT 40% = right column (tabbed: score/turn/widget/notes).
 *      Emits `data-layout="2col-60-40"`.
 *
 *   2. LEGACY 3-zone flex (Wave D.2). Triggered when `mainColumn` is absent
 *      and `leftSidebar` + `centerColumn` are provided. Preserves
 *      backward-compat for any caller still on the original API.
 *      Emits `data-layout="3col-legacy"`.
 *
 * Gate C: layout primitive, not a MeepleCard pattern.
 *
 * @see docs/superpowers/plans/2026-06-15-issue-2374-session-live-g1-3-col-layout.md §4 T2
 */

import type { ReactElement, ReactNode } from 'react';

// ─── Props ────────────────────────────────────────────────────────────────────

export interface DesktopBodyProps {
  /**
   * Main column (LEFT 60% in new 60/40 layout). Required for the 2-zone path.
   * When provided, `leftSidebar` + `centerColumn` are ignored.
   */
  readonly mainColumn?: ReactNode;
  /**
   * Right column (RIGHT 40% in new 60/40 layout, 340px aside in legacy).
   * Optional in legacy path (placeholder rendered); required-by-convention in 2-zone path.
   */
  readonly rightColumn?: ReactNode;
  /**
   * @deprecated Use {@link mainColumn} for the new 60/40 layout (G1 #2374).
   * Kept for callers still on Wave D.2 API.
   */
  readonly leftSidebar?: ReactNode;
  /**
   * @deprecated Use {@link mainColumn} for the new 60/40 layout (G1 #2374).
   * Kept for callers still on Wave D.2 API.
   */
  readonly centerColumn?: ReactNode;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function DesktopBody({
  mainColumn,
  rightColumn,
  leftSidebar,
  centerColumn,
}: DesktopBodyProps): ReactElement {
  // ─── New 2-zone 60/40 layout (G1 #2374) ────────────────────────────────────
  if (mainColumn !== undefined) {
    return (
      <div
        data-slot="desktop-body"
        data-layout="2col-60-40"
        className="hidden flex-1 overflow-hidden bg-background lg:grid lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]"
      >
        {/* LEFT 60% — main column */}
        <main className="flex flex-col overflow-y-auto p-4">{mainColumn}</main>

        {/* RIGHT 40% — right column (tabs) */}
        <aside className="flex flex-col overflow-y-auto border-l border-border/60 bg-card">
          {rightColumn}
        </aside>
      </div>
    );
  }

  // ─── Legacy 3-zone flex layout (Wave D.2 back-compat) ──────────────────────
  return (
    <div
      data-slot="desktop-body"
      data-layout="3col-legacy"
      className="hidden flex-1 overflow-hidden lg:flex"
    >
      {/* Left sidebar: 280px */}
      <aside className="w-[280px] shrink-0 overflow-y-auto border-r border-border/60 bg-card">
        {leftSidebar}
      </aside>

      {/* Center column: flex-1 */}
      <main className="flex flex-1 flex-col overflow-y-auto p-4">{centerColumn}</main>

      {/* Right column: 340px — Foundation placeholder */}
      <aside className="w-[340px] shrink-0 overflow-y-auto border-l border-border/60 bg-card">
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
