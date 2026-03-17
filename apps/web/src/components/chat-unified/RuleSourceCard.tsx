'use client';

import { useState, useCallback, useRef, useEffect } from 'react';

import { BookOpen, FileText, ExternalLink, ChevronRight } from 'lucide-react';

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
import { useAppMode } from '@/hooks/useAppMode';
import type { AppMode } from '@/lib/stores/user-preferences';
import { cn } from '@/lib/utils';
import type { Citation } from '@/types';

import { PdfPageModal } from './PdfPageModal';

// ============================================================================
// Types
// ============================================================================

export interface RuleSourceCardProps {
  citations: Citation[];
  gameTitle?: string;
  publisherUrl?: string;
  /** Override mode (defaults to useAppMode() hook) */
  mode?: AppMode;
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function getRelevanceColor(score: number): string {
  if (score >= 0.8) return 'border-l-green-500';
  if (score >= 0.5) return 'border-l-amber-500';
  return 'border-l-stone-300 dark:border-l-stone-600';
}

function formatRelevance(score: number): string {
  return `${Math.round(score * 100)}%`;
}

// ============================================================================
// Sub-components
// ============================================================================

interface CitationChipProps {
  citation: Citation;
  isActive: boolean;
  isPowerMode: boolean;
  onClick: () => void;
}

function CitationChip({ citation, isActive, isPowerMode, onClick }: CitationChipProps) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={isActive}
      onClick={onClick}
      className={cn(
        'inline-flex items-center gap-1 px-2 py-1 rounded-md text-xs transition-colors',
        'bg-orange-100 dark:bg-orange-900/40',
        'hover:bg-orange-200 dark:hover:bg-orange-800/50',
        'focus:outline-none focus:ring-2 focus:ring-orange-400/60',
        isActive && 'ring-1 ring-orange-400 font-medium',
        isPowerMode && 'border-l-2',
        isPowerMode && getRelevanceColor(citation.relevanceScore)
      )}
    >
      <span>p.{citation.pageNumber}</span>
      {isPowerMode && (
        <span className="text-[10px] opacity-75">{formatRelevance(citation.relevanceScore)}</span>
      )}
    </button>
  );
}

// ============================================================================
// Main Component
// ============================================================================

/**
 * Collapsible card showing rule citations from a game's rulebook.
 * Issue #5524: Replaces inline CitationBadge pills with a richer display.
 */
export function RuleSourceCard({
  citations,
  gameTitle,
  publisherUrl,
  mode: modeProp,
  className,
}: RuleSourceCardProps) {
  const hookMode = useAppMode();
  const effectiveMode = modeProp ?? hookMode;
  const isPowerMode = effectiveMode === 'power';

  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const [pdfModalOpen, setPdfModalOpen] = useState(false);
  const chipListRef = useRef<HTMLDivElement>(null);

  // Keyboard navigation for chips
  const handleChipKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
        e.preventDefault();
        setActiveIndex(prev => (prev + 1) % citations.length);
      } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
        e.preventDefault();
        setActiveIndex(prev => (prev - 1 + citations.length) % citations.length);
      }
    },
    [citations.length]
  );

  // Focus active chip when index changes
  useEffect(() => {
    if (!chipListRef.current) return;
    const buttons = chipListRef.current.querySelectorAll<HTMLButtonElement>('[role="tab"]');
    buttons[activeIndex]?.focus();
  }, [activeIndex]);

  // Guard: nothing to render (after all hooks to respect Rules of Hooks)
  if (!citations || citations.length === 0) return null;

  const activeCitation = citations[activeIndex] ?? citations[0];
  const isSingle = citations.length === 1;

  const headerLabel = gameTitle
    ? `${citations.length} ${citations.length === 1 ? 'fonte' : 'fonti'} dal regolamento di ${gameTitle}`
    : `${citations.length} ${citations.length === 1 ? 'fonte dal regolamento' : 'fonti dal regolamento'}`;

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <div
        className={cn(
          'mt-3 rounded-lg border',
          'bg-orange-50/50 dark:bg-orange-950/20',
          'border-orange-200/60 dark:border-orange-800/40',
          className
        )}
        data-testid="rule-source-card"
      >
        {/* Header */}
        <CollapsibleTrigger asChild>
          <button
            type="button"
            className={cn(
              'flex w-full items-center gap-2 px-3 py-2.5 text-left',
              'font-quicksand text-sm font-semibold',
              'text-orange-900 dark:text-orange-100',
              'hover:bg-orange-100/50 dark:hover:bg-orange-900/30',
              'focus:outline-none focus:ring-2 focus:ring-orange-400/60 focus:ring-inset',
              'rounded-lg transition-colors'
            )}
            aria-expanded={isOpen}
            data-testid="rule-source-header"
          >
            <ChevronRight
              className={cn(
                'h-4 w-4 flex-shrink-0 transition-transform duration-200',
                isOpen && 'rotate-90'
              )}
            />
            <BookOpen className="h-4 w-4 flex-shrink-0" />
            <span>{headerLabel}</span>
          </button>
        </CollapsibleTrigger>

        {/* Expanded content */}
        <CollapsibleContent>
          <div className="px-3 pb-3 space-y-3">
            {/* Chip row (multi-citation only) */}
            {!isSingle && (
              <div
                ref={chipListRef}
                role="tablist"
                aria-label="Citazioni"
                className="flex flex-wrap gap-1.5"
                onKeyDown={handleChipKeyDown}
                data-testid="citation-chips"
              >
                {citations.map((c, i) => (
                  <CitationChip
                    key={`${c.documentId}-${c.pageNumber}-${i}`}
                    citation={c}
                    isActive={i === activeIndex}
                    isPowerMode={isPowerMode}
                    onClick={() => setActiveIndex(i)}
                  />
                ))}
              </div>
            )}

            {/* Quote block */}
            <blockquote
              role="blockquote"
              className={cn(
                'border-l-2 border-orange-300 dark:border-orange-600',
                'pl-3 py-1',
                'text-sm italic font-nunito',
                'text-stone-700 dark:text-stone-300'
              )}
              data-testid="citation-quote"
            >
              {activeCitation.snippet ? (
                <>
                  <p>&ldquo;{activeCitation.snippet}&rdquo;</p>
                  <p className="mt-1 text-xs not-italic text-stone-500 dark:text-stone-400">
                    — Regolamento, p.{activeCitation.pageNumber}
                  </p>
                </>
              ) : (
                <p className="not-italic text-stone-500 dark:text-stone-400">
                  Pagina {activeCitation.pageNumber}
                </p>
              )}
            </blockquote>

            {/* Action row */}
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={() => setPdfModalOpen(true)}
                className={cn(
                  'inline-flex items-center gap-1 text-xs',
                  'text-orange-600 dark:text-orange-400',
                  'hover:underline focus:outline-none focus:ring-1 focus:ring-orange-400 rounded'
                )}
                data-testid="view-pdf-btn"
              >
                <FileText className="h-3.5 w-3.5" />
                Vedi nel PDF
              </button>

              {publisherUrl && (
                <a
                  href={publisherUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={cn(
                    'inline-flex items-center gap-1 text-xs',
                    'text-orange-600 dark:text-orange-400',
                    'hover:underline focus:outline-none focus:ring-1 focus:ring-orange-400 rounded'
                  )}
                  data-testid="publisher-link"
                >
                  <ExternalLink className="h-3.5 w-3.5" />
                  Regolamento ufficiale
                </a>
              )}
            </div>
          </div>
        </CollapsibleContent>
      </div>

      {/* PDF Modal (outside card layout) */}
      <PdfPageModal
        citation={activeCitation}
        open={pdfModalOpen}
        onClose={() => setPdfModalOpen(false)}
      />
    </Collapsible>
  );
}
