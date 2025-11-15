/**
 * CitationCard - Individual PDF citation display (Issue #859)
 *
 * Displays a single citation from RAG response with:
 * - Page number badge
 * - Snippet text preview
 * - Relevance score indicator (optional)
 * - Subtle card styling for visual separation
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
}

export const CitationCard = React.memo(function CitationCard({
  citation,
  showRelevanceScore = false,
  className
}: CitationCardProps) {
  const { pageNumber, snippet, relevanceScore } = citation;

  // Format relevance score as percentage
  const scorePercentage = Math.round(relevanceScore * 100);

  return (
    <Card
      className={cn(
        "border-l-4 border-l-blue-500 bg-gray-50 hover:bg-gray-100 transition-colors",
        className
      )}
      data-testid="citation-card"
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
      </CardContent>
    </Card>
  );
});
