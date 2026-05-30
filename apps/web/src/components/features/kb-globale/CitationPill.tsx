/**
 * CitationPill — page-level citation chip (D-E spec-panel 2026-05-30).
 *
 * onClick callback receives { docId, page } so the caller can push
 * `?docId=&page=` to the URL (orchestrator wires viewer scroll).
 *
 * Visual: numbered circle + ref text (e.g. "p.14 §4.1"), button-role,
 * DS-15 entity-kb tokens, jest-axe clean.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx:2195 (CitationPill)
 */

import { type JSX } from 'react';

import { cn } from '@/lib/utils';

export interface CitationPillProps {
  /** 1-based index in the citations array */
  n: number;
  /** Display text (e.g. "p.14" or "p.14 §4.1") — caller formats from KbCitation */
  refText: string;
  /** PdfDocumentId (D-E deep-link target) */
  docId: string;
  /** Page number (D-E deep-link target) */
  page: number;
  /** Required accessible label (full sentence, i18n-injected by caller) */
  ariaLabel: string;
  /** Click handler receiving deep-link payload */
  onClick?: (link: { docId: string; page: number }) => void;
  className?: string;
}

export function CitationPill({
  n,
  refText,
  docId,
  page,
  ariaLabel,
  onClick,
  className,
}: CitationPillProps): JSX.Element {
  return (
    <button
      type="button"
      data-slot="kb-globale-citation-pill"
      aria-label={ariaLabel}
      onClick={() => onClick?.({ docId, page })}
      className={cn(
        'inline-flex items-center gap-1 px-1.5 py-0 rounded-full align-baseline',
        'border border-entity-kb/25 bg-entity-kb/10 text-entity-kb',
        'font-mono text-[10px] font-bold cursor-pointer',
        'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-1',
        'transition-colors duration-150 hover:bg-entity-kb/20',
        className
      )}
    >
      <span
        className={cn(
          'inline-flex items-center justify-center',
          'w-3.5 h-3.5 rounded-full bg-entity-kb text-white text-[9px]'
        )}
      >
        {n}
      </span>
      <span>{refText}</span>
    </button>
  );
}
