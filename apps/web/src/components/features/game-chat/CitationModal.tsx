/**
 * CitationModal — preview citation con tab Snippet (default) + PDF originale (lazy).
 *
 * Tab Snippet (default): rendering testo della citation (snippet o paraphrasedSnippet
 * se copyrightTier='protected'). Pattern PR #918 invariato per il body Snippet.
 *
 * Tab PDF originale (lazy mount): delega a <CitationPdfTab> che gestisce ownership
 * gate via useCanViewPdf + isPublic shortcut. Il contenuto NON viene montato finché
 * l'utente non clicca il tab (anti-fetch).
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.1 §4.4
 */
'use client';

import { useEffect, useRef, useState } from 'react';
import type { ReactElement, ReactNode } from 'react';

import clsx from 'clsx';

import { CitationPdfTab } from '@/components/features/game-chat/CitationPdfTab';
import type { Citation } from '@/types';


export interface CitationModalProps {
  readonly citation: Citation | null;
  readonly open: boolean;
  readonly onClose: () => void;
  readonly gameId?: string;
}

type TabKind = 'snippet' | 'pdf';

export function CitationModal({
  citation,
  open,
  onClose,
  gameId,
}: CitationModalProps): ReactElement | null {
  const containerRef = useRef<HTMLDivElement>(null);
  const [activeTab, setActiveTab] = useState<TabKind>('snippet');
  const [pdfTabEverOpened, setPdfTabEverOpened] = useState(false);

  // Reset state on each open
  useEffect(() => {
    if (open) {
      setActiveTab('snippet');
      setPdfTabEverOpened(false);
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    containerRef.current?.focus();
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleEsc);
    return () => document.removeEventListener('keydown', handleEsc);
  }, [open, onClose]);

  if (!open || !citation) return null;

  const handleTabChange = (tab: TabKind) => {
    setActiveTab(tab);
    if (tab === 'pdf') setPdfTabEverOpened(true);
  };

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
        className={clsx(
          'mx-4 flex max-h-[90%] w-full max-w-[600px] flex-col',
          'overflow-hidden rounded-lg border border-border bg-card shadow-xl outline-none'
        )}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <header className="flex items-center gap-3 border-b border-border-light px-4 py-3">
          <h2 id="citation-modal-title" className="text-lg font-bold">
            📖 p. {citation.pageNumber}
          </h2>
          <span className="flex-1" />
          <button
            type="button"
            aria-label="Chiudi"
            onClick={onClose}
            className="rounded-sm p-1 text-muted-foreground hover:bg-muted"
          >
            ×
          </button>
        </header>

        {/* Tabs */}
        <nav className="flex gap-0 border-b border-border-light px-4" role="tablist">
          <TabButton kind="snippet" active={activeTab} onSelect={handleTabChange}>
            📝 Snippet
          </TabButton>
          <TabButton kind="pdf" active={activeTab} onSelect={handleTabChange}>
            📄 PDF originale
          </TabButton>
        </nav>

        {/* Tab body */}
        <div className="flex flex-1 flex-col gap-3 overflow-y-auto p-4">
          {activeTab === 'snippet' && (
            <blockquote
              className={clsx(
                'rounded-r-md border-l-4 px-4 py-3 italic',
                'border-l-[hsl(var(--c-kb))] bg-[hsl(var(--c-kb)/0.05)]',
                'text-base leading-relaxed text-foreground'
              )}
            >
              {displayedSnippet || (
                <span className="text-sm not-italic text-muted-foreground">
                  Nessuna anteprima disponibile.
                </span>
              )}
            </blockquote>
          )}

          {activeTab === 'pdf' && pdfTabEverOpened && (
            <CitationPdfTab
              documentId={citation.documentId}
              gameId={gameId}
              initialPage={citation.pageNumber}
              isPublic={citation.isPublic}
            />
          )}
        </div>

        {/* Footer */}
        <footer className="flex items-center gap-2 border-t border-border-light bg-card px-4 py-3">
          <span className="flex-1" />
          <button
            type="button"
            onClick={onClose}
            className={clsx(
              'rounded-full bg-[hsl(var(--c-chat))] px-4 py-1.5',
              'text-sm font-semibold text-white hover:opacity-90'
            )}
          >
            Chiudi
          </button>
        </footer>
      </div>
    </div>
  );
}

interface TabButtonProps {
  readonly kind: TabKind;
  readonly active: TabKind;
  readonly onSelect: (kind: TabKind) => void;
  readonly children: ReactNode;
}

function TabButton({ kind, active, onSelect, children }: TabButtonProps): ReactElement {
  const isSelected = active === kind;
  return (
    <button
      type="button"
      role="tab"
      aria-selected={isSelected}
      onClick={() => onSelect(kind)}
      className={clsx(
        'relative inline-flex items-center gap-1.5 px-4 py-3',
        'font-semibold text-sm transition-colors',
        isSelected
          ? 'text-[hsl(var(--c-kb))] after:absolute after:inset-x-3 after:-bottom-px after:h-0.5 after:rounded-full after:bg-[hsl(var(--c-kb))]'
          : 'text-muted-foreground hover:text-foreground'
      )}
    >
      {children}
    </button>
  );
}
