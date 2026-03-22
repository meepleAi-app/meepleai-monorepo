'use client';

import { type ReactNode } from 'react';

interface SessionSheetProps {
  isOpen: boolean;
  onClose: () => void;
  children: ReactNode;
}

/**
 * Slide-in sheet panel for in-session card details.
 * Renders as a fixed overlay on mobile; a sidebar on md+.
 */
export function SessionSheet({ isOpen, onClose, children }: SessionSheetProps) {
  if (!isOpen) return null;

  return (
    <div
      data-testid="session-sheet"
      className="fixed inset-0 z-50 flex md:relative md:inset-auto"
      role="dialog"
      aria-modal="true"
    >
      {/* Backdrop (mobile only) */}
      <div
        data-testid="sheet-overlay"
        className="absolute inset-0 bg-black/40 md:hidden"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Panel */}
      <div
        data-testid="sheet-panel"
        className={[
          'relative ml-auto flex h-full w-full max-w-md flex-col',
          'bg-background shadow-xl',
          'md:static md:h-auto md:w-80 md:rounded-xl md:border md:border-border',
        ].join(' ')}
      >
        {/* Drag handle (mobile) */}
        <div
          data-testid="sheet-drag-handle"
          className="mx-auto mt-2 h-1 w-10 rounded-full bg-muted md:hidden"
          aria-hidden="true"
        />

        {/* Close button */}
        <button
          type="button"
          onClick={onClose}
          className="absolute right-3 top-3 rounded-md p-1 text-muted-foreground hover:text-foreground"
          aria-label="Close panel"
        >
          ✕
        </button>

        <div className="overflow-y-auto p-4 pt-10">{children}</div>
      </div>
    </div>
  );
}
