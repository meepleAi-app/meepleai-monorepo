/**
 * CitationModal — preview citation con snippet + page number.
 *
 * Hybrid C (Q4 brainstorm): footer button "Apri nella KB" condizionale —
 * hidden quando `onOpenInKb === undefined` (G4 non ancora atterrato).
 * Quando G4 sarà pronto, basta passare il callback dal consumer.
 *
 * Spec: §3.3 §4.3
 */
'use client';

import { useEffect, useRef } from 'react';
import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { Citation } from '@/types';

export interface CitationModalProps {
  readonly citation: Citation | null;
  readonly open: boolean;
  readonly onClose: () => void;
  readonly onOpenInKb?: () => void;
}

export function CitationModal({
  citation,
  open,
  onClose,
  onOpenInKb,
}: CitationModalProps): ReactElement | null {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    // focus il container all'apertura per a11y
    containerRef.current?.focus();
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleEsc);
    return () => document.removeEventListener('keydown', handleEsc);
  }, [open, onClose]);

  if (!open || !citation) return null;

  // Per copyright 'protected', preferisci il paraphrasedSnippet se disponibile
  const displayedSnippet =
    citation.copyrightTier === 'protected' && citation.paraphrasedSnippet
      ? citation.paraphrasedSnippet
      : citation.snippet;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="citation-modal-title"
      data-slot="citation-modal"
      className={clsx(
        'fixed inset-0 z-50 flex items-center justify-center',
        'bg-black/50 backdrop-blur-sm'
      )}
      onClick={onClose}
    >
      <div
        ref={containerRef}
        tabIndex={-1}
        className="max-w-lg w-full mx-4 rounded-lg bg-card border border-border shadow-xl outline-none"
        onClick={e => e.stopPropagation()}
      >
        <header className="border-b border-border-light px-4 py-3">
          <h2 id="citation-modal-title" className="font-bold text-lg">
            📖 p. {citation.pageNumber}
          </h2>
        </header>
        <div className="p-4">
          {displayedSnippet ? (
            <blockquote className="border-l-4 border-[hsl(var(--c-kb))] pl-4 text-sm text-foreground">
              {displayedSnippet}
            </blockquote>
          ) : (
            <p className="text-sm text-muted-foreground italic">Nessuna anteprima disponibile.</p>
          )}
        </div>
        <footer className="flex items-center justify-end gap-2 border-t border-border-light px-4 py-3">
          {onOpenInKb && (
            <button
              type="button"
              onClick={onOpenInKb}
              className="rounded-full border border-[hsl(var(--c-kb))] px-3 py-1.5 text-sm font-semibold text-[hsl(var(--c-kb))] hover:bg-[hsl(var(--c-kb)/0.1)]"
            >
              📖 Apri nella KB
            </button>
          )}
          <button
            type="button"
            onClick={onClose}
            className="rounded-full bg-[hsl(var(--c-chat))] px-4 py-1.5 text-sm font-semibold text-white hover:opacity-90"
          >
            Chiudi
          </button>
        </footer>
      </div>
    </div>
  );
}
