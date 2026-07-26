/* eslint-disable local/no-hardcoded-color-utility -- glass pill bg-white/85 follows the mockup .e-bg pattern; entity-neutral surface for action affordance. */
'use client';

/**
 * Hover-visible glass placeholder for card actions (3-dot menu).
 *
 * **No functional handler** — this is a visual-only placeholder matching the SP4
 * mockup at `admin-mockups/design_files/sp4-library-desktop.jsx:709-721`. Click
 * stops propagation so it doesn't trigger the parent card's `onClick`. Future
 * issue may wire a consumer-defined menu action via a prop.
 *
 * Rendered as a NON-interactive `<div>` (not a `<button>`) because the consumer
 * card surface (GridCard) is itself a `<button>`, and a nested `<button>` trips
 * axe's `nested-interactive` rule regardless of `aria-hidden` (that rule is
 * structural — it fires on interactive-in-interactive nesting, and aria-hidden
 * only exempts contrast/name audits, not nesting) (#3289). `aria-hidden` still
 * removes this decorative element from the a11y tree so axe stops color-contrast
 * auditing its semi-transparent glass over varying card covers. A plain `<div>`
 * is not a focusable/interactive control, so there is no tab-order affordance to
 * suppress and no nesting violation. When a real `onActionsMenu` handler is
 * wired in a follow-up, promote it back to a `<button>` rendered OUTSIDE the card
 * button (or make the card a non-button surface) to keep it AA-clean.
 *
 * See #1856 DEC-4, #3289.
 */
export function MenuPlaceholder() {
  return (
    <div
      data-slot="menu-placeholder"
      aria-hidden="true"
      onClick={e => e.stopPropagation()}
      className="absolute right-2 top-2 z-10 flex h-6 w-6 items-center justify-center rounded-md border-none bg-white/85 text-sm font-extrabold text-foreground opacity-0 backdrop-blur-md transition-opacity duration-200 group-hover:opacity-100"
    >
      ⋯
    </div>
  );
}
