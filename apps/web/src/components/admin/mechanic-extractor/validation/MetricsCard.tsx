/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 37)
 *
 * Display card for a `MechanicAnalysisMetricsDto` snapshot:
 *  - Four percentage scores (coverage / page accuracy / BGG match / overall)
 *  - Certification badge (Certified | NotCertified | NotEvaluated)
 *  - Drift warning when the snapshot was computed against a stale golden version
 *
 * All percentages are formatted culture-independently (`(value * 100).toFixed(0)%`).
 */

'use client';

import { AlertTriangleIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import type {
  CertificationStatus,
  MechanicAnalysisMetricsDto,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

// ──────────────────────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────────────────────

/**
 * Render a 0..1 ratio as a culture-independent integer percentage string.
 * No locale-aware formatting — `toFixed(0)` always yields ASCII digits with
 * a `%` suffix, which is what the backend metrics contract expects.
 */
function formatPercent(value: number): string {
  return `${(value * 100).toFixed(0)}%`;
}

function first8(hash: string): string {
  return hash.length <= 8 ? hash : hash.slice(0, 8);
}

const CERTIFICATION_BADGE_CLASS: Record<CertificationStatus, string> = {
  Certified: 'border-green-300 bg-green-50 text-green-800 dark:bg-green-950/30 dark:text-green-300',
  NotCertified: 'border-rose-300 bg-rose-50 text-rose-800 dark:bg-rose-950/30 dark:text-rose-300',
  NotEvaluated:
    'border-slate-300 bg-slate-50 text-slate-700 dark:bg-zinc-900/40 dark:text-zinc-300',
};

const CERTIFICATION_LABEL: Record<CertificationStatus, string> = {
  Certified: 'Certified',
  NotCertified: 'Not certified',
  NotEvaluated: 'Not evaluated',
};

// ──────────────────────────────────────────────────────────────────────────
// Component
// ──────────────────────────────────────────────────────────────────────────

export interface MetricsCardProps {
  metrics: MechanicAnalysisMetricsDto;
  /**
   * Current golden version hash for the shared game. When provided AND it
   * differs from `metrics.goldenVersionHash`, a drift warning is shown.
   */
  currentGoldenVersionHash?: string;
}

export function MetricsCard({ metrics, currentGoldenVersionHash }: MetricsCardProps) {
  const driftDetected =
    !!currentGoldenVersionHash &&
    !!metrics.goldenVersionHash &&
    currentGoldenVersionHash !== metrics.goldenVersionHash;

  return (
    <Card
      data-testid="mechanic-metrics-card"
      className="bg-white/70 backdrop-blur-md border-slate-200/60 dark:bg-zinc-800/70 dark:border-zinc-700/60"
    >
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between gap-3 flex-wrap">
          <CardTitle className="font-quicksand text-base">AI Comprehension Metrics</CardTitle>
          <Badge
            variant="outline"
            className={CERTIFICATION_BADGE_CLASS[metrics.certificationStatus]}
            data-testid="metrics-certification-badge"
          >
            {CERTIFICATION_LABEL[metrics.certificationStatus]}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {driftDetected && (
          <Alert
            variant="default"
            data-testid="metrics-drift-warning"
            className="border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-800/50 dark:bg-amber-950/20 dark:text-amber-200"
          >
            <AlertTriangleIcon className="h-4 w-4" />
            <AlertTitle>Stale metrics</AlertTitle>
            <AlertDescription>
              Metrics computed against golden v{first8(metrics.goldenVersionHash)}; current golden
              is v{first8(currentGoldenVersionHash ?? '')}. Re-evaluate to refresh.
            </AlertDescription>
          </Alert>
        )}

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <ScorePill label="Coverage" value={metrics.coveragePct} testId="metric-coverage" />
          <ScorePill
            label="Page accuracy"
            value={metrics.pageAccuracyPct}
            testId="metric-page-accuracy"
          />
          <ScorePill label="BGG match" value={metrics.bggMatchPct} testId="metric-bgg-match" />
          <ScorePill
            label="Overall score"
            value={metrics.overallScore}
            testId="metric-overall"
            emphasis
          />
        </div>
      </CardContent>
    </Card>
  );
}

// ──────────────────────────────────────────────────────────────────────────
// Internal: single score pill
// ──────────────────────────────────────────────────────────────────────────

interface ScorePillProps {
  label: string;
  value: number;
  testId: string;
  emphasis?: boolean;
}

function ScorePill({ label, value, testId, emphasis }: ScorePillProps) {
  return (
    <div
      className={
        'rounded-lg border border-slate-200 bg-white p-3 text-center transition-shadow hover:shadow-sm dark:border-zinc-700 dark:bg-zinc-800'
      }
    >
      <div
        data-testid={testId}
        className={
          emphasis
            ? 'font-quicksand text-2xl font-bold text-amber-500'
            : 'font-quicksand text-xl font-semibold text-foreground'
        }
      >
        {formatPercent(value)}
      </div>
      <div className="mt-0.5 text-xs text-muted-foreground">{label}</div>
    </div>
  );
}
