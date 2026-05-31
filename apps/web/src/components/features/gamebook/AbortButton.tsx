'use client';

import { type ReactElement } from 'react';

import { LABELS } from './TranslateViewer.steps';

export interface AbortButtonProps {
  /**
   * Callback fired when user clicks the abort button.
   * Caller should handle FSM rollback (phase change, sse.stop(), etc.)
   * per DEC-3.
   */
  onClick: () => void;
}

/**
 * AbortButton component
 *
 * Single button rendered conditionally by the parent (TranslateViewer) when
 * uiStep is abortable (ocr, translating, glossary-check).
 *
 * Per DEC-6: Responsive Tailwind layout
 * - Mobile (default): fixed bottom-4 right-4, rounded-full (FAB style)
 * - Desktop (lg:): static position, rounded-md, no shadow
 *
 * Uses semantic button with aria-label for accessibility.
 * Focus ring uses --c-game color variable (game entity color).
 */
export function AbortButton({ onClick }: AbortButtonProps): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      data-testid="translate-abort-button"
      aria-label={LABELS.abortAriaLabel}
      className="
        fixed bottom-4 right-4 z-50 rounded-full shadow-lg px-4 py-3
        bg-background border border-border text-foreground
        lg:static lg:inline-flex lg:rounded-md lg:shadow-none lg:px-3 lg:py-1.5
        hover:bg-muted focus-visible:ring-2 focus-visible:ring-[var(--c-game)]
      "
    >
      {LABELS.abort}
    </button>
  );
}
