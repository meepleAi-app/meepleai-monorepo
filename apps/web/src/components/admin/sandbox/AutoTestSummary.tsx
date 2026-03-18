'use client';

import { CheckCircle2, XCircle, Clock, BarChart3, Timer } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import type { AutoTestResult } from './types';

interface AutoTestSummaryProps {
  results: AutoTestResult[];
}

function getScoreBadgeStyle(passed: number, total: number): string {
  if (passed >= 6) return 'border-green-400 bg-green-50 text-green-700';
  if (passed >= 4) return 'border-amber-400 bg-amber-50 text-amber-700';
  return 'border-red-400 bg-red-50 text-red-700';
}

export function AutoTestSummary({ results }: AutoTestSummaryProps) {
  const passedCount = results.filter(r => r.passed).length;
  const totalCount = results.length;
  const avgConfidence = results.reduce((sum, r) => sum + r.confidence, 0) / totalCount;
  const avgLatency = results.reduce((sum, r) => sum + r.latencyMs, 0) / totalCount;
  const totalTime = results.reduce((sum, r) => sum + r.latencyMs, 0);
  const hasFailed = passedCount < totalCount;

  return (
    <div
      className="rounded-xl border bg-white/70 backdrop-blur-md p-4 space-y-4"
      data-testid="auto-test-summary"
    >
      {/* Header with overall score */}
      <div className="flex items-center justify-between">
        <h3 className="font-quicksand font-semibold text-sm">Risultati Auto-Test</h3>
        <Badge
          variant="outline"
          className={`text-sm font-semibold px-2.5 py-0.5 ${getScoreBadgeStyle(passedCount, totalCount)}`}
          data-testid="overall-score"
        >
          {passedCount}/{totalCount}
        </Badge>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-3 gap-3 text-xs font-nunito">
        <div className="flex flex-col items-center gap-1 rounded-lg border bg-gray-50/50 p-2">
          <BarChart3 className="h-3.5 w-3.5 text-muted-foreground" />
          <span className="text-muted-foreground">Confidenza media</span>
          <span className="font-semibold tabular-nums" data-testid="avg-confidence">
            {(avgConfidence * 100).toFixed(0)}%
          </span>
        </div>
        <div className="flex flex-col items-center gap-1 rounded-lg border bg-gray-50/50 p-2">
          <Clock className="h-3.5 w-3.5 text-muted-foreground" />
          <span className="text-muted-foreground">Latenza media</span>
          <span className="font-semibold tabular-nums" data-testid="avg-latency">
            {Math.round(avgLatency)}ms
          </span>
        </div>
        <div className="flex flex-col items-center gap-1 rounded-lg border bg-gray-50/50 p-2">
          <Timer className="h-3.5 w-3.5 text-muted-foreground" />
          <span className="text-muted-foreground">Tempo totale</span>
          <span className="font-semibold tabular-nums" data-testid="total-time">
            {(totalTime / 1000).toFixed(1)}s
          </span>
        </div>
      </div>

      {/* Per-question list */}
      <div className="space-y-1" data-testid="question-results">
        {results.map((result, i) => (
          <div
            key={i}
            className="flex items-center gap-2 rounded-md px-2 py-1.5 text-xs font-nunito hover:bg-gray-50"
          >
            {result.passed ? (
              <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-green-600" />
            ) : (
              <XCircle className="h-3.5 w-3.5 shrink-0 text-red-500" />
            )}
            <span className="flex-1">{result.question}</span>
            <span className="tabular-nums text-muted-foreground">
              {(result.confidence * 100).toFixed(0)}%
            </span>
          </div>
        ))}
      </div>

      {/* Recommendation for failed questions */}
      {hasFailed && (
        <div
          className="rounded-md border border-amber-200 bg-amber-50/50 px-3 py-2 text-xs font-nunito text-amber-800"
          data-testid="recommendation"
        >
          Verifica i chunk recuperati per le domande fallite
        </div>
      )}
    </div>
  );
}
