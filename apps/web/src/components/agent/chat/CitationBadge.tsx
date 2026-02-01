/**
 * CitationBadge - Clickable citation badge (Issue #3244)
 *
 * Displays source reference with icon, page number, and score.
 * Shows snippet on hover via tooltip.
 */

import React from 'react';

import { FileText } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

interface CitationBadgeProps {
  /** Citation data */
  citation: Citation;

  /** Click handler (logs for MVP, future: scroll to PDF page) */
  onClick?: (citation: Citation) => void;
}

export function CitationBadge({ citation, onClick }: CitationBadgeProps): React.JSX.Element {
  const handleClick = () => {
    onClick?.(citation);
  };

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <button
            className="inline-flex items-center gap-1.5 px-2 py-1 bg-gray-700 border border-gray-600 rounded text-xs hover:bg-gray-600 transition-colors focus:outline-none focus:ring-2 focus:ring-cyan-400"
            onClick={handleClick}
            type="button"
          >
            <FileText className="h-3 w-3 text-cyan-400" />
            <span className="text-cyan-400">{citation.source || 'Source'}</span>
            {(citation.pageNumber ?? citation.page) && (
              <span className="text-gray-400">p.{citation.pageNumber ?? citation.page}</span>
            )}
            {(citation.score ?? citation.relevanceScore) && (
              <span className="text-gray-500 text-[10px]">
                ({((citation.score ?? citation.relevanceScore ?? 0) * 100).toFixed(0)}%)
              </span>
            )}
          </button>
        </TooltipTrigger>
        {(citation.snippet ?? citation.text) && (
          <TooltipContent className="max-w-xs bg-gray-900 text-gray-100 border-gray-700">
            <p className="text-xs">{citation.snippet ?? citation.text}</p>
          </TooltipContent>
        )}
      </Tooltip>
    </TooltipProvider>
  );
}
