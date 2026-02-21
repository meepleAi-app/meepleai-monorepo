'use client';

import { useState } from 'react';

import { FileText } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { Citation } from '@/types';

import { PdfPageModal } from './PdfPageModal';

interface CitationBadgeProps {
  citation: Citation;
  /** Additional class names */
  className?: string;
}

/**
 * Clickable citation badge that opens a PDF page viewer modal.
 * Issue #4919: Citations as clickable links to source PDF page.
 */
export function CitationBadge({ citation, className }: CitationBadgeProps) {
  const [isOpen, setIsOpen] = useState(false);

  const tooltip = citation.snippet
    ? `"${citation.snippet.slice(0, 120)}${citation.snippet.length > 120 ? '...' : ''}"`
    : `Pagina ${citation.pageNumber}`;

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        title={tooltip}
        aria-label={`Apri citazione — pagina ${citation.pageNumber}`}
        className={cn(
          'inline-flex items-center gap-0.5 text-[10px] px-1.5 py-0.5 rounded',
          'bg-orange-500/20 text-orange-700 dark:text-orange-300',
          'hover:bg-orange-500/40 focus:outline-none focus:ring-1 focus:ring-orange-400',
          'transition-colors cursor-pointer',
          className
        )}
      >
        <FileText className="w-2.5 h-2.5 flex-shrink-0" />
        p.{citation.pageNumber}
      </button>

      <PdfPageModal
        citation={citation}
        open={isOpen}
        onClose={() => setIsOpen(false)}
      />
    </>
  );
}
