'use client';

import { BottomSheet } from '@/components/ui/overlays/BottomSheet';

export interface CitationData {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
  copyrightTier: 'full' | 'protected';
  paraphrasedSnippet?: string;
}

interface CitationSheetProps {
  open: boolean;
  citation: CitationData | null;
  onOpenChange: (open: boolean) => void;
}

/**
 * Bottom sheet that displays citation details including page number,
 * snippet (or paraphrased version for protected content), and relevance score.
 */
export function CitationSheet({ open, citation, onOpenChange }: CitationSheetProps) {
  if (!citation) return null;

  const displaySnippet =
    citation.copyrightTier === 'protected' && citation.paraphrasedSnippet
      ? citation.paraphrasedSnippet
      : citation.snippet;

  const relevancePercent = Math.round(citation.relevanceScore * 100);

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title={`Pagina ${citation.pageNumber}`}>
      <div className="flex flex-col gap-4">
        <blockquote className="rounded-lg border border-white/10 bg-white/5 px-4 py-3 text-sm italic text-[var(--gaming-text-primary,white)]">
          {displaySnippet}
        </blockquote>

        <p className="text-xs text-[var(--gaming-text-secondary,rgba(255,255,255,0.6))]">
          Rilevanza: <span className="font-medium text-amber-400">{relevancePercent}%</span>
        </p>
      </div>
    </BottomSheet>
  );
}
