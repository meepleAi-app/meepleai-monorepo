'use client';

import { useState, useCallback, useRef, useEffect } from 'react';

import { BookOpen, FileText, ExternalLink, ChevronRight, Lock } from 'lucide-react';

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

/** Resolve copyrightTier with backward-compat default */
function getTier(citation: Citation): 'full' | 'protected' {
  return citation.copyrightTier ?? 'full';
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
  const tier = getTier(citation);
  const isFull = tier === 'full';

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
        isActive && 'ring-1 font-medium',
        isActive && isFull && 'ring-[hsl(174,60%,40%)]',
        isActive && !isFull && 'ring-amber-500',
        isPowerMode && 'border-l-2',
        isPowerMode && isFull && 'border-l-[hsl(174,60%,40%)]',
        isPowerMode && !isFull && 'border-l-amber-500',
        isPowerMode && !isFull && !isActive && getRelevanceColor(citation.relevanceScore),
        isPowerMode && isFull && !isActive && getRelevanceColor(citation.relevanceScore)
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
 * Copyright tier support: full (verbatim) vs protected (paraphrased).
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
  const activeTier = getTier(activeCitation);
  const isFull = activeTier === 'full';
  const hasAnyProtected = citations.some(c => getTier(c) === 'protected');

  const headerLabel = gameTitle
    ? `${citations.length} ${citations.length === 1 ? 'fonte' : 'fonti'} dal regolamento di ${gameTitle}`
    : `${citations.length} ${citations.length === 1 ? 'fonte dal regolamento' : 'fonti dal regolamento'}`;

  // Determine quote content for active citation
  const quoteContent = isFull ? activeCitation.snippet : activeCitation.paraphrasedSnippet || null;

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
            {hasAnyProtected && (
              <Lock
                className="h-3.5 w-3.5 flex-shrink-0 text-amber-600 dark:text-amber-400"
                data-testid="copyright-lock-icon"
              />
            )}
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
                'border-l-2 pl-3 py-1',
                'text-sm font-nunito',
                'text-stone-700 dark:text-stone-300',
                isFull ? 'border-l-[hsl(174,60%,40%)] italic' : 'border-l-amber-500'
              )}
              data-testid="citation-quote"
            >
              {/* Tier label */}
              <p
                className={cn(
                  'text-[9px] uppercase tracking-wider font-semibold mb-1',
                  isFull ? 'text-[hsl(174,60%,40%)]' : 'text-amber-600 dark:text-amber-400'
                )}
                data-testid="citation-tier-label"
              >
                {isFull ? 'Citazione originale' : 'Riformulazione AI'}
              </p>

              {/* Quote text */}
              {isFull ? (
                // Full tier: verbatim snippet
                activeCitation.snippet ? (
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
                )
              ) : // Protected tier: paraphrase or page-only fallback
              quoteContent ? (
                <>
                  <p>{quoteContent}</p>
                  <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
                    — Regolamento, p.{activeCitation.pageNumber}
                  </p>
                </>
              ) : (
                <p className="text-stone-500 dark:text-stone-400">
                  Vedi pagina {activeCitation.pageNumber} del regolamento
                </p>
              )}
            </blockquote>

            {/* Action row */}
            <div className="flex flex-col gap-2">
              <div className="flex items-center gap-3">
                {/* "Vedi nel PDF" only for full tier */}
                {isFull && (
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
                )}

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

              {/* Upsell CTA for protected tier */}
              {!isFull && (
                <div
                  className="inline-flex items-center gap-1.5 text-xs text-stone-500"
                  data-testid="upsell-cta"
                >
                  <Lock className="h-3 w-3 flex-shrink-0" />
                  <span>
                    {activeCitation.isPublic
                      ? 'Dichiara possesso per accesso completo'
                      : 'Carica il regolamento per la versione completa'}
                  </span>
                </div>
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
