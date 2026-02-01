/**
 * CitationBadge - Clickable citation badge (Issue #3244)
 * Issue #3251 (FRONT-015) - PDF viewer integration
 *
 * Displays source reference with icon, page number, and score.
 * Shows snippet on hover via tooltip.
 * Clicks open PDF viewer at specific page.
 */

import React from 'react';

import { FileText } from 'lucide-react';
import { toast } from 'sonner';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';
import { useAgentStore } from '@/stores/agentStore';

interface CitationBadgeProps {
  /** Citation data */
  citation: Citation;

  /** Document ID for PDF viewer */
  documentId?: string;

  /** Click handler (optional - PDF viewer is default) */
  onClick?: (citation: Citation) => void;
}

export function CitationBadge({ citation, documentId, onClick }: CitationBadgeProps): React.JSX.Element {
  const { openPdfViewer } = useAgentStore();

  const pageNumber = citation.pageNumber ?? citation.page ?? 1;

  const handleClick = () => {
    // Custom handler takes precedence
    if (onClick) {
      onClick(citation);
      return;
    }

    // Open PDF viewer at citation page
    if (documentId) {
      openPdfViewer(documentId, pageNumber);
      toast.info(`📄 Apertura ${citation.source || 'regolamento'} p.${pageNumber}`, {
        duration: 2000,
      });
    }
  };

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <button
            className="inline-flex items-center gap-1.5 px-2 py-1 bg-orange-50 dark:bg-gray-700 border border-orange-200 dark:border-gray-600 rounded text-xs text-orange-700 dark:text-cyan-400 hover:bg-orange-100 dark:hover:bg-gray-600 hover:border-orange-300 dark:hover:border-gray-500 transition-all hover:-translate-y-0.5 focus:outline-none focus:ring-2 focus:ring-cyan-400"
            onClick={handleClick}
            type="button"
            aria-label={`Apri ${citation.source || 'fonte'} alla pagina ${pageNumber}`}
          >
            <FileText className="h-3 w-3" />
            <span>{citation.source || 'Source'}</span>
            {(citation.pageNumber ?? citation.page) && (
              <span className="text-orange-500 dark:text-gray-400">p.{pageNumber}</span>
            )}
            {(citation.score ?? citation.relevanceScore) && (
              <span className="text-orange-400 dark:text-gray-500 text-[10px]">
                ({((citation.score ?? citation.relevanceScore ?? 0) * 100).toFixed(0)}%)
              </span>
            )}
          </button>
        </TooltipTrigger>
        <TooltipContent className="max-w-xs bg-gray-900 text-gray-100 border-gray-700">
          {(citation.snippet ?? citation.text) && (
            <p className="text-xs mb-1">{citation.snippet ?? citation.text}</p>
          )}
          <p className="text-[10px] text-cyan-400">
            Click per aprire alla pagina {pageNumber}
          </p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
