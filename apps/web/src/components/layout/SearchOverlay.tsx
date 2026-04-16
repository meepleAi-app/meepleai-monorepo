'use client';

import React, { useEffect, useRef } from 'react';

import { Search, X } from 'lucide-react';

export interface SearchOverlayProps {
  open: boolean;
  onClose: () => void;
}

/**
 * SearchOverlay
 *
 * Expandable full-width search bar rendered as an overlay.
 * Replaces TopBarSearchPill as part of nav simplification.
 * Full search results will be added in a later iteration.
 */
export function SearchOverlay({ open, onClose }: SearchOverlayProps): React.ReactElement | null {
  const inputRef = useRef<HTMLInputElement>(null);

  // Auto-focus input whenever the overlay opens
  useEffect(() => {
    if (!open) return;
    const raf = requestAnimationFrame(() => {
      inputRef.current?.focus();
    });
    return () => cancelAnimationFrame(raf);
  }, [open]);

  if (!open) return null;

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') {
      onClose();
    }
  };

  return (
    <>
      {/* Semi-transparent backdrop */}
      <div
        data-testid="search-overlay-backdrop"
        className="fixed inset-0 z-40 bg-black/40"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Search bar */}
      <div
        data-testid="search-overlay-bar"
        className="fixed inset-x-0 top-0 z-50 flex items-center gap-3 bg-background px-4 py-3 shadow-md"
        onClick={e => e.stopPropagation()}
      >
        <Search className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />

        <input
          ref={inputRef}
          type="search"
          role="searchbox"
          placeholder="Cerca giochi, sessioni, giocatori..."
          className="min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          onKeyDown={handleKeyDown}
        />

        <button
          type="button"
          aria-label="Chiudi ricerca"
          onClick={onClose}
          className="flex items-center justify-center rounded-md p-1 text-muted-foreground transition-colors hover:text-foreground"
        >
          <X className="h-5 w-5" />
        </button>
      </div>
    </>
  );
}
