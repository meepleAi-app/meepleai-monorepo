/* eslint-disable local/no-hardcoded-color-utility -- glass pill bg-white/85 follows the mockup .e-bg pattern; entity-neutral surface for action affordance. */
'use client';

/**
 * Hover-visible glass button placeholder for card actions (3-dot menu).
 *
 * **No functional handler** — this is a visual-only placeholder matching the SP4
 * mockup at `admin-mockups/design_files/sp4-library-desktop.jsx:709-721`. Click
 * stops propagation so it doesn't trigger the parent card's `onClick`. Future
 * issue may wire a consumer-defined menu action via a prop.
 *
 * `tabIndex={-1}` removes the button from the keyboard tab order — without
 * this, keyboard / screen-reader users would land on a non-functional "Azioni"
 * button on every one of the 72 consumer card surfaces. Restore to default
 * (`tabIndex={0}`) when a real `onActionsMenu` handler prop is added.
 *
 * See #1856 DEC-4.
 */
export function MenuPlaceholder() {
  return (
    <button
      type="button"
      aria-label="Azioni"
      tabIndex={-1}
      onClick={e => e.stopPropagation()}
      className="absolute right-2 top-2 z-10 flex h-6 w-6 items-center justify-center rounded-md border-none bg-white/85 text-sm font-extrabold text-foreground opacity-0 backdrop-blur-md transition-opacity duration-200 group-hover:opacity-100"
    >
      ⋯
    </button>
  );
}
