/**
 * CitationCard - Individual PDF citation display (Issue #859, BGAI-074)
 *
 * Displays a single citation from RAG response with:
 * - Page number badge
 * - Snippet text preview
 * - Relevance score indicator (optional)
 * - Subtle card styling for visual separation
 * - Click to jump to PDF page (BGAI-074)
 *
 * Uses Shadcn/UI Card component for consistent design.
 */

import React from 'react';
import { Citation } from '@/types';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

interface CitationCardProps {
  citation: Citation;
  showRelevanceScore?: boolean;
  className?: string;
  onClick?: (citation: Citation) => void;
}

export const CitationCard = React.memo(function CitationCard({
  citation,
  showRelevanceScore = false,
  className,
  onClick
}: CitationCardProps) {
  const { pageNumber, snippet, relevanceScore } = citation;

  // Format relevance score as percentage
  const scorePercentage = Math.round(relevanceScore * 100);

  const handleClick = () => {
    onClick?.(citation);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick?.(citation);
    }
  };

  const isClickable = !!onClick;

  return (
    <Card
      className={cn(
        "border-l-4 border-l-blue-500 bg-gray-50 hover:bg-gray-100 transition-colors",
        isClickable && "cursor-pointer hover:shadow-md active:shadow-sm",
        className
      )}
      data-testid="citation-card"
      onClick={isClickable ? handleClick : undefined}
      onKeyDown={isClickable ? handleKeyDown : undefined}
      tabIndex={isClickable ? 0 : undefined}
      role={isClickable ? 'button' : undefined}
      aria-label={isClickable ? `View citation from page ${pageNumber}` : undefined}
    >
      <CardContent className="p-3">
        <div className="flex items-start justify-between gap-2 mb-2">
          {/* Page Number Badge */}
          <span
            className="inline-flex items-center px-2 py-1 bg-blue-100 text-blue-800 rounded text-xs font-medium flex-shrink-0"
            data-testid="citation-page"
            aria-label={`Page ${pageNumber}`}
          >
            Pag. {pageNumber}
            {isClickable && <span className="ml-1">📄</span>}
          </span>

          {/* Relevance Score (optional) */}
          {showRelevanceScore && (
            <span
              className="text-xs text-gray-600"
              data-testid="citation-score"
              aria-label={`Relevance ${scorePercentage}%`}
              title={`Relevance score: ${scorePercentage}%`}
            >
              {scorePercentage}% rilevante
            </span>
          )}
        </div>

        {/* Snippet Text */}
        <p
          className="text-sm text-gray-700 leading-relaxed line-clamp-3"
          data-testid="citation-snippet"
        >
          "{snippet}"
        </p>

        {isClickable && (
          <p className="text-xs text-blue-600 mt-2">
            Clicca per visualizzare nel PDF
          </p>
        )}
      </CardContent>
    </Card>
  );
});
