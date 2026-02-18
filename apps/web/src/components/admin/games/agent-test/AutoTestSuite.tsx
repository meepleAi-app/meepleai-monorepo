'use client';

/**
 * Auto Test Suite
 * Runs 8 standard board game questions against the RAG agent and displays a quality report.
 */

import {
  CheckCircle2Icon,
  XCircleIcon,
  FlaskConicalIcon,
  LoaderCircleIcon,
  TrophyIcon,
  ClockIcon,
  TargetIcon,
  BarChart3Icon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

import {
  useAgentAutoTest,
  type AgentAutoTestResult,
  type TestCaseResult,
  type AgentQualityReport,
} from '@/hooks/queries/useAgentTesting';

// ─── Types ───────────────────────────────────────────────────────────────────

interface AutoTestSuiteProps {
  gameId: string;
}

// ─── Grade Badge ─────────────────────────────────────────────────────────────

function GradeBadge({ grade }: { grade: string }) {
  const gradeStyles: Record<string, string> = {
    A: 'bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-400 ring-green-500/20',
    B: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400 ring-blue-500/20',
    C: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 ring-amber-500/20',
    F: 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400 ring-red-500/20',
  };

  return (
    <span
      className={`inline-flex h-12 w-12 items-center justify-center rounded-full text-xl font-bold ring-2 ${
        gradeStyles[grade] ?? gradeStyles.F
      }`}
    >
      {grade}
    </span>
  );
}

// ─── Quality Report Card ─────────────────────────────────────────────────────

function QualityReportCard({ report }: { report: AgentQualityReport }) {
  return (
    <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center justify-between text-base">
          <span className="flex items-center gap-2">
            <BarChart3Icon className="h-4 w-4 text-amber-500" />
            Quality Report
          </span>
          <GradeBadge grade={report.overallGrade} />
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <div className="space-y-1">
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <TrophyIcon className="h-3.5 w-3.5" />
              Pass Rate
            </div>
            <p className="text-lg font-semibold">
              {Math.round(report.passRate * 100)}%
            </p>
            <p className="text-xs text-muted-foreground">
              {report.passed}/{report.totalTests} passed
            </p>
          </div>
          <div className="space-y-1">
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <TargetIcon className="h-3.5 w-3.5" />
              Avg Confidence
            </div>
            <p className="text-lg font-semibold">
              {(report.averageConfidence * 100).toFixed(1)}%
            </p>
          </div>
          <div className="space-y-1">
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <ClockIcon className="h-3.5 w-3.5" />
              Avg Latency
            </div>
            <p className="text-lg font-semibold">
              {report.averageLatencyMs < 1000
                ? `${report.averageLatencyMs}ms`
                : `${(report.averageLatencyMs / 1000).toFixed(1)}s`}
            </p>
          </div>
          <div className="space-y-1">
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <CheckCircle2Icon className="h-3.5 w-3.5" />
              Tests
            </div>
            <p className="text-lg font-semibold">{report.totalTests}</p>
            <p className="text-xs text-muted-foreground">
              <span className="text-green-600">{report.passed} pass</span>{' '}
              <span className="text-red-600">{report.failed} fail</span>
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ─── Test Case Row ───────────────────────────────────────────────────────────

function TestCaseRow({ testCase }: { testCase: TestCaseResult }) {
  return (
    <div className="space-y-2 rounded-lg border border-slate-200 dark:border-zinc-700 p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-2 min-w-0">
          {testCase.passed ? (
            <CheckCircle2Icon className="h-5 w-5 text-green-500 mt-0.5 shrink-0" />
          ) : (
            <XCircleIcon className="h-5 w-5 text-red-500 mt-0.5 shrink-0" />
          )}
          <div className="min-w-0">
            <p className="text-sm font-medium text-foreground">{testCase.question}</p>
            {testCase.failureReason && (
              <p className="text-xs text-red-500 mt-0.5">{testCase.failureReason}</p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-3 text-xs text-muted-foreground shrink-0">
          <span>{(testCase.confidenceScore * 100).toFixed(0)}% conf</span>
          <span>{testCase.latencyMs}ms</span>
          <span>{testCase.chunksRetrieved} chunks</span>
        </div>
      </div>
      {testCase.answer && (
        <div className="ml-7 rounded-md bg-slate-50 dark:bg-zinc-900 p-3">
          <p className="text-sm text-muted-foreground line-clamp-3">{testCase.answer}</p>
        </div>
      )}
    </div>
  );
}

// ─── Main Component ──────────────────────────────────────────────────────────

export function AutoTestSuite({ gameId }: AutoTestSuiteProps) {
  const { mutate: runTest, data: result, isPending } = useAgentAutoTest();

  return (
    <div className="space-y-6">
      {/* Run button */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm text-muted-foreground">
            Sends 8 standard board game questions to test RAG retrieval quality, confidence, and latency.
          </p>
        </div>
        <Button
          onClick={() => runTest(gameId)}
          disabled={isPending}
          className="bg-amber-500 hover:bg-amber-600 text-white"
        >
          {isPending ? (
            <>
              <LoaderCircleIcon className="h-4 w-4 animate-spin mr-2" />
              Running Tests...
            </>
          ) : (
            <>
              <FlaskConicalIcon className="h-4 w-4 mr-2" />
              Run Auto Test
            </>
          )}
        </Button>
      </div>

      {/* Loading state */}
      {isPending && (
        <Card className="bg-amber-50 dark:bg-amber-950/20 border-amber-200 dark:border-amber-800/40">
          <CardContent className="p-6 flex items-center justify-center gap-3">
            <LoaderCircleIcon className="h-5 w-5 animate-spin text-amber-500" />
            <p className="text-sm text-amber-700 dark:text-amber-400">
              Running test suite... This may take 30-60 seconds.
            </p>
          </CardContent>
        </Card>
      )}

      {/* Results */}
      {result && <TestResults result={result} />}
    </div>
  );
}

function TestResults({ result }: { result: AgentAutoTestResult }) {
  return (
    <div className="space-y-4">
      <QualityReportCard report={result.report} />

      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Test Cases</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {result.testCases.map((tc) => (
            <TestCaseRow key={tc.index} testCase={tc} />
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
