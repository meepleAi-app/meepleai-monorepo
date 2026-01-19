/**
 * ResponseCard - RAG response display component (Issue #1002, BGAI-062)
 *
 * Displays Q&A agent responses with:
 * - Answer text
 * - Confidence score badge (color-coded by threshold)
 * - Citations from RAG pipeline
 * - Low quality warning indicator
 *
 * Uses Shadcn/UI Card + Badge components for consistent design.
 * Aligns with backend QaResponseDto structure.
 */

import React from 'react';

import { CitationList } from '@/components/citations/CitationList';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';
import type { Citation } from '@/types';

interface ResponseCardProps {
  /** The AI-generated answer text */
  answer: string;

  /** Overall confidence score (0-1). Primary metric for display */
  overallConfidence?: number;

  /** RAG citations from PDF documents */
  citations?: Citation[];

  /** Flag indicating response quality is below threshold */
  isLowQuality?: boolean;

  /** Show relevance scores on citations */
  showCitationScores?: boolean;

  /** Optional CSS class */
  className?: string;

  /** Callback when citation is clicked (for PDF navigation) */
  onCitationClick?: (citation: Citation) => void;
}

/**
 * Get confidence level indicator
 * Based on RAG validation thresholds (≥0.70 = good)
 */
function getConfidenceLevel(score: number): {
  label: string;
  color: string;
  bgColor: string;
  textColor: string;
} {
  if (score >= 0.76) {
    return {
      label: 'Alta confidenza',
      color: 'green',
      bgColor: 'bg-green-100',
      textColor: 'text-green-800',
    };
  }
  if (score >= 0.51) {
    return {
      label: 'Media confidenza',
      color: 'yellow',
      bgColor: 'bg-yellow-100',
      textColor: 'text-yellow-800',
    };
  }
  return {
    label: 'Bassa confidenza',
    color: 'red',
    bgColor: 'bg-red-100',
    textColor: 'text-red-800',
  };
}

export const ResponseCard = React.memo(function ResponseCard({
  answer,
  overallConfidence,
  citations,
  isLowQuality = false,
  showCitationScores = false,
  className,
  onCitationClick,
}: ResponseCardProps) {
  // Format confidence score as percentage
  const confidencePercent =
    overallConfidence !== undefined ? Math.round(overallConfidence * 100) : null;

  const confidenceLevel =
    confidencePercent !== null && overallConfidence !== undefined
      ? getConfidenceLevel(overallConfidence)
      : null;

  return (
    <Card className={cn('border-l-4 border-l-blue-500', className)} data-testid="response-card">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-900">Risposta</h3>

          {/* Confidence Badge */}
          {confidencePercent !== null && confidenceLevel && (
            <Badge
              className={cn(
                'px-2 py-1 text-xs font-medium',
                confidenceLevel.bgColor,
                confidenceLevel.textColor
              )}
              data-testid="confidence-badge"
              aria-label={`Confidence ${confidencePercent}%`}
            >
              {confidencePercent}% - {confidenceLevel.label}
            </Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-3">
        {/* Low Quality Warning */}
        {isLowQuality && (
          <Alert variant="destructive" data-testid="low-quality-warning">
            <AlertDescription>
              ⚠️ Questa risposta potrebbe non soddisfare gli standard di qualità. Verifica le fonti
              e consulta le regole originali del gioco.
            </AlertDescription>
          </Alert>
        )}

        {/* Answer Text */}
        <div
          className="text-sm text-gray-800 leading-relaxed whitespace-pre-wrap"
          data-testid="answer-text"
        >
          {answer}
        </div>

        {/* Citations */}
        {citations && citations.length > 0 && (
          <CitationList
            citations={citations}
            showRelevanceScores={showCitationScores}
            collapsible={true}
            onCitationClick={onCitationClick}
          />
        )}
      </CardContent>
    </Card>
  );
});
