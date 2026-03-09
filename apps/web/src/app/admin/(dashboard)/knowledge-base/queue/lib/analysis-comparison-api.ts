import { useQuery } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';

// ── Types ─────────────────────────────────────────────────────────────

export interface ListDiff<T> {
  added: T[];
  removed: T[];
  unchanged: T[];
}

export interface FaqDiffItem {
  question: string;
  answer: string;
  confidence: number;
}

export interface FaqModified {
  question: string;
  leftAnswer: string;
  rightAnswer: string;
  leftConfidence: number;
  rightConfidence: number;
}

export interface FaqDiff {
  added: FaqDiffItem[];
  removed: FaqDiffItem[];
  modified: FaqModified[];
  unchanged: FaqDiffItem[];
}

export interface AnalysisComparisonDto {
  leftId: string;
  rightId: string;
  leftVersion: string;
  rightVersion: string;
  leftAnalyzedAt: string;
  rightAnalyzedAt: string;
  confidenceScoreDelta: number;
  mechanicsDiff: ListDiff<string>;
  commonQuestionsDiff: ListDiff<string>;
  keyConceptsDiff: ListDiff<string>;
  faqDiff: FaqDiff;
  summaryChanged: boolean;
  leftSummary: string | null;
  rightSummary: string | null;
}

// ── API Functions ────────────────────────────────────────────────────

export async function getAnalysisComparison(
  leftId: string,
  rightId: string
): Promise<AnalysisComparisonDto> {
  const result = await apiClient.get<AnalysisComparisonDto>(
    `/api/v1/admin/analysis/${leftId}/compare/${rightId}`
  );
  return result!;
}

// ── React Query Hook ─────────────────────────────────────────────────

export function useAnalysisComparison(leftId: string | null, rightId: string | null) {
  return useQuery({
    queryKey: ['admin', 'analysis', 'compare', leftId, rightId],
    queryFn: () => getAnalysisComparison(leftId!, rightId!),
    enabled: !!leftId && !!rightId,
    staleTime: 60_000,
  });
}
