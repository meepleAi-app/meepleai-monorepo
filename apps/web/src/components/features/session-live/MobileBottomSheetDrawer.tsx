'use client';

/**
 * MobileBottomSheetDrawer — Issue #2374 G1 (T9).
 *
 * Mobile bottom-sheet drawer wrapping Radix UI Dialog primitive
 * (`@/components/ui/navigation/sheet`) with `side="bottom"`. Hosts the
 * 4 polymorphic tabs (Score/Turn/Widget/Notes) on mobile, mirroring the
 * desktop RIGHT 40% column. Triggered by a floating action button in
 * `MobileBody` (see T9 spec in plan §4 / mockup parts.jsx L266–286).
 *
 * §5 frozen contract for downstream consumers:
 *   - data-slot="mobile-bottom-sheet" (E2E selector, never rename)
 *   - data-slot="mobile-bottom-sheet-drag-handle" (visual cue, aria-hidden)
 *   - data-slot="mobile-bottom-sheet-close" (close button)
 *   - role="dialog" + aria-modal="true" via Radix Dialog primitive
 *   - role="tablist" + role="tab" buttons in tab strip
 *   - Backdrop click closes drawer (Radix default)
 *   - Escape closes drawer (Radix default)
 *   - Focus trap inside dialog (Radix default)
 *
 * Token discipline (§3 D-4):
 *   - semantic tokens: bg-card / bg-muted / text-foreground / border-border
 *   - entity utilities: text-entity-session / border-entity-session on active tab
 *   - NO raw HSL literals.
 *
 * Reference mockup: admin-mockups/design_files/sp4-session-skeleton-parts.jsx L266–286.
 */

import type { ReactElement, ReactNode } from 'react';

import { Sheet, SheetContent, SheetPortal, SheetTitle } from '@/components/ui/navigation/sheet';

import type { LiveTab } from './RightColumnTabs';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface MobileBottomSheetDrawerLabels {
  /** Drawer title (sr-only Radix Dialog title for a11y). */
  readonly drawerTitle: string;
  /** aria-label for the close button. */
  readonly closeAriaLabel: string;
  /** aria-label for the role="tablist" tab strip. */
  readonly tabsAriaLabel: string;
  readonly tabScore: string;
  readonly tabTurn: string;
  readonly tabWidget: string;
  readonly tabNotes: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface MobileBottomSheetDrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly activeTab: LiveTab;
  readonly onTabChange: (next: LiveTab) => void;
  readonly children: ReactNode;
  readonly labels: MobileBottomSheetDrawerLabels;
}

// ─── Tab config ───────────────────────────────────────────────────────────────

const ORDERED_TABS: ReadonlyArray<{ id: LiveTab; labelKey: keyof MobileBottomSheetDrawerLabels }> =
  [
    { id: 'score', labelKey: 'tabScore' },
    { id: 'turn', labelKey: 'tabTurn' },
    { id: 'widget', labelKey: 'tabWidget' },
    { id: 'notes', labelKey: 'tabNotes' },
  ];

// ─── Component ────────────────────────────────────────────────────────────────

export function MobileBottomSheetDrawer({
  open,
  onOpenChange,
  activeTab,
  onTabChange,
  children,
  labels,
}: MobileBottomSheetDrawerProps): ReactElement {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetPortal>
        <SheetContent
          side="bottom"
          data-slot="mobile-bottom-sheet"
          className="h-[84%] gap-0 rounded-t-2xl border-t-2 border-entity-session
            bg-card p-0 lg:hidden"
        >
          {/* sr-only title — required by Radix Dialog a11y contract */}
          <SheetTitle className="sr-only">{labels.drawerTitle}</SheetTitle>

          {/* Drag handle — visual cue (aria-hidden, no interaction) */}
          <div
            className="flex shrink-0 items-center justify-center pt-2"
            data-slot="mobile-bottom-sheet-drag-handle"
            aria-hidden="true"
          >
            <div className="h-1 w-10 rounded-full bg-border-strong" />
          </div>

          {/* Header — close button (Radix portals its own absolute Close in SheetContent;
              we render an explicit labelled close to surface the i18n aria-label). */}
          <div className="flex shrink-0 items-center justify-end gap-2 px-3 py-2">
            <button
              type="button"
              data-slot="mobile-bottom-sheet-close"
              onClick={() => onOpenChange(false)}
              aria-label={labels.closeAriaLabel}
              className="flex h-7 w-7 items-center justify-center rounded-md
                border border-border bg-muted text-muted-foreground
                transition-colors hover:bg-card hover:text-foreground
                focus-visible:outline-none focus-visible:ring-2
                focus-visible:ring-ring"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>

          {/* Tab strip — role="tablist" with 4 buttons */}
          <div
            role="tablist"
            aria-label={labels.tabsAriaLabel}
            aria-orientation="horizontal"
            className="flex shrink-0 gap-1 overflow-x-auto border-b border-border
              bg-muted/50 px-2 py-1.5"
          >
            {ORDERED_TABS.map(tab => {
              const isActive = activeTab === tab.id;
              const label = labels[tab.labelKey];
              return (
                <button
                  key={tab.id}
                  type="button"
                  role="tab"
                  data-slot="mobile-bottom-sheet-tab"
                  data-tab={tab.id}
                  aria-selected={isActive}
                  tabIndex={isActive ? 0 : -1}
                  onClick={() => onTabChange(tab.id)}
                  className={[
                    'shrink-0 rounded-full border px-3 py-1.5 text-xs font-semibold',
                    'transition-colors focus-visible:outline-none',
                    'focus-visible:ring-2 focus-visible:ring-ring',
                    isActive
                      ? 'border-entity-session bg-entity-session/10 text-entity-session'
                      : 'border-border bg-card text-muted-foreground hover:text-foreground',
                  ].join(' ')}
                >
                  {label}
                </button>
              );
            })}
          </div>

          {/* Tab content (scrollable) */}
          <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">{children}</div>
        </SheetContent>
      </SheetPortal>
    </Sheet>
  );
}
