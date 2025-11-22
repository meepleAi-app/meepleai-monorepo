/**
 * CitationList - Collection of citations display (Issue #859, BGAI-074)
 *
 * Renders a list of PDF citations with:
 * - Header label
 * - Empty state handling
 * - Collapsible UI for better UX
 * - Grid layout for multiple citations
 * - Click to jump to PDF page (BGAI-074)
 *
 * Usage: Display citations under assistant messages in chat
 */

import React, { useState } from 'react';
import { Citation } from '@/types';
import { CitationCard } from './CitationCard';
import { cn } from '@/lib/utils';

interface CitationListProps {
  citations: Citation[];
  showRelevanceScores?: boolean;
  collapsible?: boolean;
  className?: string;
  onCitationClick?: (citation: Citation) => void;
}

export const CitationList = React.memo(function CitationList({
  citations,
  showRelevanceScores = false,
  collapsible = false,
  className,
  onCitationClick
}: CitationListProps) {
  // Always start expanded (collapsible only affects toggle ability)
  const [isExpanded, setIsExpanded] = useState(true);

  // Empty state
  if (!citations || citations.length === 0) {
    return null;
  }

  const toggleExpanded = () => {
    if (collapsible) {
      setIsExpanded(!isExpanded);
    }
  };

  return (
    <div className={cn("mt-3", className)} data-testid="citation-list">
      {/* Header */}
      <button
        onClick={toggleExpanded}
        disabled={!collapsible}
        className={cn(
          "flex items-center gap-2 mb-2 text-sm font-medium text-gray-700",
          collapsible && "cursor-pointer hover:text-gray-900"
        )}
        aria-expanded={isExpanded}
        aria-controls="citations-content"
        data-testid="citations-header"
      >
        {collapsible && (
          <span className="text-gray-500" aria-hidden="true">
            {isExpanded ? '▼' : '▶'}
          </span>
        )}
        <span>
          📚 Fonti ({citations.length})
        </span>
      </button>

      {/* Citations Grid */}
      {isExpanded && (
        <div
          id="citations-content"
          className="grid gap-2 sm:grid-cols-1 md:grid-cols-2"
          data-testid="citations-content"
        >
          {citations.map((citation, index) => (
            <CitationCard
              key={`${citation.documentId}-${citation.pageNumber}-${index}`}
              citation={citation}
              showRelevanceScore={showRelevanceScores}
              onClick={onCitationClick}
            />
          ))}
        </div>
      )}
    </div>
  );
});
