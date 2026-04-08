import type { JSX } from 'react';

import { Sparkles, FileText } from 'lucide-react';

interface GameKbBadgeProps {
  isIndexed: boolean;
  isLoading?: boolean;
}

/**
 * Badge che indica se un gioco ha un PDF indicizzato (RAG agent disponibile).
 * - isIndexed=true → "AI pronto" (verde, icon sparkles)
 * - isIndexed=false → "Solo manuale" (grigio, icon file)
 * - isLoading → null (nessun render)
 */
export function GameKbBadge({
  isIndexed,
  isLoading = false,
}: GameKbBadgeProps): JSX.Element | null {
  if (isLoading) return null;

  if (isIndexed) {
    return (
      <span
        data-testid="game-kb-badge"
        data-indexed="true"
        className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-nunito font-medium text-emerald-700 border border-emerald-200"
      >
        <Sparkles className="h-3 w-3" aria-hidden="true" />
        AI pronto
      </span>
    );
  }

  return (
    <span
      data-testid="game-kb-badge"
      data-indexed="false"
      className="inline-flex items-center gap-1 rounded-full bg-gray-50 px-2 py-0.5 text-xs font-nunito font-medium text-gray-600 border border-gray-200"
    >
      <FileText className="h-3 w-3" aria-hidden="true" />
      Solo manuale
    </span>
  );
}
