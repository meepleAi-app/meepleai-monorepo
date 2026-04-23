'use client';

/**
 * Mechanic Analyses — M1.2 Async Pipeline UI (ISSUE-524 / ADR-051).
 *
 * Consumes the admin endpoints exposed by {@link AdminMechanicAnalysesEndpoints}:
 *   POST /admin/mechanic-analyses                   → 202 Accepted
 *   GET  /admin/mechanic-analyses/{id}/status       → polling target
 *   POST /admin/mechanic-analyses/{id}/submit-review
 *   POST /admin/mechanic-analyses/{id}/approve
 *   POST /admin/mechanic-analyses/{id}/suppress
 *
 * Flow:
 *   1. Admin picks a SharedGame + PDF and sets a cost cap (with optional override).
 *   2. Generate → 202 Accepted → analysisId captured; React Query polls /status every 2s
 *      while the pipeline is running (status=Draft without all section runs complete).
 *   3. Lifecycle buttons are enabled based on status (Draft/Rejected → submit-review;
 *      InReview → approve; any state → suppress via AlertDialog).
 */

import { useMemo, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangleIcon,
  CheckIcon,
  ClockIcon,
  DollarSignIcon,
  Loader2Icon,
  PlayIcon,
  SendIcon,
  ShieldAlertIcon,
  SparklesIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { createSharedGamesClient } from '@/lib/api/clients/sharedGamesClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import {
  MECHANIC_ANALYSIS_STATUS_LABELS,
  MECHANIC_SECTION_LABELS,
  MECHANIC_SECTION_RUN_STATUS_LABELS,
  MechanicAnalysisStatus,
  SUPPRESSION_REQUEST_SOURCE_LABELS,
  SuppressionRequestSource,
  type MechanicAnalysisStatusDto,
  type SuppressionRequestSourceValue,
} from '@/lib/api/schemas/mechanic-analyses.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });
const gamesClient = createSharedGamesClient({ httpClient });

const STATUS_BADGE_CLASS: Record<number, string> = {
  [MechanicAnalysisStatus.Draft]: 'bg-slate-100 text-slate-700 border-slate-300',
  [MechanicAnalysisStatus.InReview]: 'bg-amber-100 text-amber-800 border-amber-300',
  [MechanicAnalysisStatus.Published]: 'bg-green-100 text-green-800 border-green-300',
  [MechanicAnalysisStatus.Rejected]: 'bg-rose-100 text-rose-800 border-rose-300',
};

const RUN_STATUS_BADGE_CLASS: Record<number, string> = {
  0: 'bg-green-100 text-green-800 border-green-300', // Succeeded
  1: 'bg-rose-100 text-rose-800 border-rose-300', // Failed
  2: 'bg-slate-100 text-slate-700 border-slate-300', // SkippedDueToCostCap
};

function formatCurrency(value: number | null | undefined): string {
  if (value == null) return '—';
  return `$${value.toFixed(4)}`;
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}

function isPipelineRunning(status: MechanicAnalysisStatusDto | null | undefined): boolean {
  if (!status) return false;
  // Draft + 0 section runs = queued; Draft + incomplete runs = running.
  // Once any terminal status is reached (InReview/Published/Rejected) we stop polling.
  if (status.status !== MechanicAnalysisStatus.Draft) return false;
  if (status.sectionRuns.length === 0) return true;
  // All 6 sections should complete; stop when each has a completedAt.
  return status.sectionRuns.length < 6;
}

export default function MechanicAnalysesPage() {
  const queryClient = useQueryClient();

  // ========== Form state ==========
  const [selectedGameId, setSelectedGameId] = useState('');
  const [selectedPdfId, setSelectedPdfId] = useState('');
  const [costCapUsd, setCostCapUsd] = useState<string>('0.50');
  const [overrideEnabled, setOverrideEnabled] = useState(false);
  const [overrideCapUsd, setOverrideCapUsd] = useState<string>('1.00');
  const [overrideReason, setOverrideReason] = useState('');
  const [generateError, setGenerateError] = useState<string | null>(null);

  // The analysis id becomes the source of truth for all polling and lifecycle actions.
  const [analysisId, setAnalysisId] = useState<string | null>(null);

  // ========== Suppress dialog state ==========
  const [suppressOpen, setSuppressOpen] = useState(false);
  const [suppressReason, setSuppressReason] = useState('');
  const [suppressSource, setSuppressSource] = useState<SuppressionRequestSourceValue>(
    SuppressionRequestSource.Email
  );
  const [lifecycleError, setLifecycleError] = useState<string | null>(null);

  // ========== Queries: games + PDFs ==========
  const { data: gamesData, isLoading: isGamesLoading } = useQuery({
    queryKey: ['shared-games', 'all'],
    queryFn: () => gamesClient.getAll({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  });

  const { data: readyPdfsData, isLoading: isReadyPdfsLoading } = useQuery({
    queryKey: ['admin', 'pdfs', 'ready-all'],
    queryFn: () =>
      adminClient.getAllPdfs({
        state: 'Ready',
        page: 1,
        pageSize: 500,
      }),
    staleTime: 60_000,
  });

  const gameIdsWithPdf = useMemo(
    () =>
      new Set((readyPdfsData?.items ?? []).map(p => p.gameId).filter((id): id is string => !!id)),
    [readyPdfsData]
  );

  const gamesWithPdf = useMemo(
    () => (gamesData?.items ?? []).filter((g: { id: string }) => gameIdsWithPdf.has(g.id)),
    [gamesData, gameIdsWithPdf]
  );

  const { data: pdfsData } = useQuery({
    queryKey: ['admin', 'pdfs', { gameId: selectedGameId }],
    queryFn: () =>
      adminClient.getAllPdfs({
        gameId: selectedGameId,
        state: 'Ready',
        page: 1,
        pageSize: 50,
      }),
    enabled: !!selectedGameId,
    staleTime: 30_000,
  });

  // ========== Polling: analysis status ==========
  const statusQuery = useQuery<MechanicAnalysisStatusDto | null>({
    queryKey: ['mechanic-analysis', analysisId],
    queryFn: () => adminClient.getMechanicAnalysisStatus(analysisId!),
    enabled: !!analysisId,
    // Poll every 2s while the pipeline is running.
    refetchInterval: query => (isPipelineRunning(query.state.data) ? 2000 : false),
  });

  const status = statusQuery.data ?? null;

  // ========== Mutations: lifecycle ==========
  const generateMutation = useMutation({
    mutationFn: async () => {
      const cap = Number.parseFloat(costCapUsd);
      if (!Number.isFinite(cap) || cap <= 0) {
        throw new Error('Cost cap must be a positive number');
      }
      return adminClient.generateMechanicAnalysis({
        sharedGameId: selectedGameId,
        pdfDocumentId: selectedPdfId,
        costCapUsd: cap,
        costCapOverride: overrideEnabled
          ? {
              newCapUsd: Number.parseFloat(overrideCapUsd),
              reason: overrideReason,
            }
          : undefined,
      });
    },
    onSuccess: res => {
      setAnalysisId(res.id);
      setGenerateError(null);
      queryClient.invalidateQueries({ queryKey: ['mechanic-analysis', res.id] });
    },
    onError: (e: unknown) => {
      const msg = e instanceof Error ? e.message : 'Generation failed';
      setGenerateError(msg);
    },
  });

  const submitReviewMutation = useMutation({
    mutationFn: () => adminClient.submitMechanicAnalysisForReview(analysisId!),
    onSuccess: () => {
      setLifecycleError(null);
      queryClient.invalidateQueries({ queryKey: ['mechanic-analysis', analysisId] });
    },
    onError: (e: unknown) => {
      setLifecycleError(e instanceof Error ? e.message : 'Submit for review failed');
    },
  });

  const approveMutation = useMutation({
    mutationFn: () => adminClient.approveMechanicAnalysis(analysisId!),
    onSuccess: () => {
      setLifecycleError(null);
      queryClient.invalidateQueries({ queryKey: ['mechanic-analysis', analysisId] });
    },
    onError: (e: unknown) => {
      setLifecycleError(e instanceof Error ? e.message : 'Approve failed');
    },
  });

  const suppressMutation = useMutation({
    mutationFn: () =>
      adminClient.suppressMechanicAnalysis(analysisId!, {
        reason: suppressReason,
        requestSource: suppressSource,
      }),
    onSuccess: () => {
      setLifecycleError(null);
      setSuppressOpen(false);
      setSuppressReason('');
      queryClient.invalidateQueries({ queryKey: ['mechanic-analysis', analysisId] });
    },
    onError: (e: unknown) => {
      setLifecycleError(e instanceof Error ? e.message : 'Suppress failed');
    },
  });

  // ========== Derived lifecycle guards ==========
  const canSubmitReview =
    !!status &&
    !status.isSuppressed &&
    (status.status === MechanicAnalysisStatus.Draft ||
      status.status === MechanicAnalysisStatus.Rejected) &&
    status.claimsCount > 0 &&
    !isPipelineRunning(status);

  const canApprove =
    !!status && !status.isSuppressed && status.status === MechanicAnalysisStatus.InReview;

  const canSuppress = !!status && !status.isSuppressed;

  const canGenerate =
    !!selectedGameId &&
    !!selectedPdfId &&
    costCapUsd !== '' &&
    (!overrideEnabled || (!!overrideReason && overrideReason.length >= 20)) &&
    !generateMutation.isPending;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Mechanic Analyses (M1.2)
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Enqueue the asynchronous AI pipeline over a rulebook PDF and monitor its lifecycle.
        </p>
        <Badge
          variant="outline"
          className="mt-2 border-sky-300 bg-sky-50 text-sky-800 dark:bg-sky-950/30 dark:text-sky-300"
        >
          <SparklesIcon className="mr-1 h-3 w-3" />
          ISSUE-524 / ADR-051 — 6 sections, cost-cap enforced
        </Badge>
      </div>

      {/* Generation form */}
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
        <CardContent className="pt-6 space-y-4">
          <h2 className="text-lg font-medium font-quicksand">Start a new analysis</h2>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-foreground">
                Shared Game
              </label>
              <Select
                value={selectedGameId}
                onValueChange={v => {
                  setSelectedGameId(v);
                  setSelectedPdfId('');
                }}
              >
                <SelectTrigger disabled={isGamesLoading || isReadyPdfsLoading}>
                  <SelectValue
                    placeholder={
                      isGamesLoading || isReadyPdfsLoading
                        ? 'Loading…'
                        : gamesWithPdf.length === 0
                          ? 'No games with Ready PDFs'
                          : 'Choose a game…'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {gamesWithPdf.map((g: { id: string; title: string }) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-foreground">PDF</label>
              <Select
                value={selectedPdfId}
                onValueChange={setSelectedPdfId}
                disabled={!selectedGameId}
              >
                <SelectTrigger>
                  <SelectValue
                    placeholder={selectedGameId ? 'Choose a PDF…' : 'Pick a game first'}
                  />
                </SelectTrigger>
                <SelectContent>
                  {pdfsData?.items?.map((p: { id: string; fileName: string }) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.fileName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-foreground">
                Cost cap (USD)
              </label>
              <input
                type="number"
                min="0.01"
                step="0.01"
                value={costCapUsd}
                onChange={e => setCostCapUsd(e.target.value)}
                className="w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-amber-400 focus:outline-none focus:ring-1 focus:ring-amber-400"
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Sections skip after total projected cost × 1.0 exceeds this cap.
              </p>
            </div>

            <div>
              <label className="mb-1.5 flex items-center gap-2 text-sm font-medium text-foreground">
                <input
                  type="checkbox"
                  checked={overrideEnabled}
                  onChange={e => setOverrideEnabled(e.target.checked)}
                  className="h-4 w-4 rounded border-slate-300"
                />
                Apply planning-time override
              </label>
              {overrideEnabled && (
                <div className="space-y-2">
                  <input
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={overrideCapUsd}
                    onChange={e => setOverrideCapUsd(e.target.value)}
                    placeholder="Override cap USD"
                    className="w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-amber-400 focus:outline-none focus:ring-1 focus:ring-amber-400"
                  />
                  <textarea
                    value={overrideReason}
                    onChange={e => setOverrideReason(e.target.value)}
                    placeholder="Reason for override (min 20 chars)"
                    className="h-20 w-full resize-y rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-amber-400 focus:outline-none focus:ring-1 focus:ring-amber-400"
                  />
                  <p className="text-xs text-muted-foreground">
                    {overrideReason.length} / 20+ characters
                  </p>
                </div>
              )}
            </div>
          </div>

          {generateError && (
            <div className="rounded-md border border-rose-300 bg-rose-50 p-2 text-sm text-rose-800">
              <AlertTriangleIcon className="mr-1 inline h-4 w-4" />
              {generateError}
            </div>
          )}

          <div className="flex gap-2">
            <Button
              onClick={() => generateMutation.mutate()}
              disabled={!canGenerate}
              className="bg-amber-500 hover:bg-amber-600 text-white"
            >
              {generateMutation.isPending ? (
                <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
              ) : (
                <PlayIcon className="mr-1 h-4 w-4" />
              )}
              Generate
            </Button>
            {analysisId && (
              <Button
                variant="outline"
                onClick={() => {
                  setAnalysisId(null);
                  setLifecycleError(null);
                }}
              >
                Clear current analysis
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Status + telemetry */}
      {analysisId && (
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-6 space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 className="text-lg font-medium font-quicksand">Analysis status</h2>
                <p className="text-xs text-muted-foreground">ID: {analysisId}</p>
              </div>
              {status && (
                <div className="flex flex-wrap items-center gap-2">
                  <Badge
                    variant="outline"
                    className={STATUS_BADGE_CLASS[status.status] ?? ''}
                    data-testid="analysis-status-badge"
                  >
                    {MECHANIC_ANALYSIS_STATUS_LABELS[status.status] ?? String(status.status)}
                  </Badge>
                  {status.isSuppressed && (
                    <Badge
                      variant="outline"
                      className="border-rose-400 bg-rose-50 text-rose-800"
                      data-testid="analysis-suppressed-badge"
                    >
                      <ShieldAlertIcon className="mr-1 h-3 w-3" />
                      Suppressed
                    </Badge>
                  )}
                  {isPipelineRunning(status) && (
                    <Badge variant="outline" className="border-sky-300 bg-sky-50 text-sky-800">
                      <Loader2Icon className="mr-1 h-3 w-3 animate-spin" />
                      Running
                    </Badge>
                  )}
                </div>
              )}
            </div>

            {statusQuery.isLoading && !status && (
              <p className="text-sm text-muted-foreground">Loading status…</p>
            )}

            {status && (
              <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                  <div className="text-muted-foreground">Prompt version</div>
                  <div className="font-medium">{status.promptVersion}</div>
                </div>
                <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                  <div className="text-muted-foreground">Model</div>
                  <div className="font-medium">
                    {status.provider} · {status.modelUsed}
                  </div>
                </div>
                <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                  <div className="text-muted-foreground flex items-center gap-1">
                    <SparklesIcon className="h-3 w-3" />
                    Tokens
                  </div>
                  <div className="font-medium">{status.totalTokensUsed.toLocaleString()}</div>
                </div>
                <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                  <div className="text-muted-foreground flex items-center gap-1">
                    <DollarSignIcon className="h-3 w-3" />
                    Cost / cap
                  </div>
                  <div className="font-medium">
                    {formatCurrency(status.estimatedCostUsd)} / {formatCurrency(status.costCapUsd)}
                    {status.costCapOverrideApplied && (
                      <span className="ml-1 text-amber-600">(override)</span>
                    )}
                  </div>
                </div>
                <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                  <div className="text-muted-foreground">Claims</div>
                  <div className="font-medium">{status.claimsCount}</div>
                </div>
                <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                  <div className="text-muted-foreground">Created</div>
                  <div className="font-medium">{formatDate(status.createdAt)}</div>
                </div>
                {status.reviewedAt && (
                  <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                    <div className="text-muted-foreground">Reviewed</div>
                    <div className="font-medium">{formatDate(status.reviewedAt)}</div>
                  </div>
                )}
                {status.suppressedAt && (
                  <div className="rounded-md border border-slate-200 p-2 text-xs dark:border-zinc-700">
                    <div className="text-muted-foreground">Suppressed</div>
                    <div className="font-medium">{formatDate(status.suppressedAt)}</div>
                  </div>
                )}
              </div>
            )}

            {status?.rejectionReason && (
              <div className="rounded-md border border-rose-300 bg-rose-50 p-2 text-sm text-rose-800">
                <strong>Rejection:</strong> {status.rejectionReason}
              </div>
            )}
            {status?.suppressionReason && (
              <div className="rounded-md border border-rose-400 bg-rose-50 p-2 text-sm text-rose-800">
                <strong>Suppression reason:</strong> {status.suppressionReason}
              </div>
            )}

            {/* Section runs table */}
            {status && status.sectionRuns.length > 0 && (
              <div>
                <h3 className="mb-2 text-sm font-medium">Section runs</h3>
                <div className="overflow-x-auto rounded-md border border-slate-200 dark:border-zinc-700">
                  <table className="w-full text-xs">
                    <thead className="bg-slate-50 dark:bg-zinc-900">
                      <tr>
                        <th className="p-2 text-left">Section</th>
                        <th className="p-2 text-left">#</th>
                        <th className="p-2 text-left">Provider/model</th>
                        <th className="p-2 text-right">Tokens</th>
                        <th className="p-2 text-right">Cost</th>
                        <th className="p-2 text-right">Latency</th>
                        <th className="p-2 text-left">Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {status.sectionRuns.map((run, idx) => (
                        <tr
                          key={`${run.section}-${run.runOrder}-${idx}`}
                          className="border-t border-slate-200 dark:border-zinc-700"
                        >
                          <td className="p-2">
                            {MECHANIC_SECTION_LABELS[run.section] ?? run.section}
                          </td>
                          <td className="p-2">{run.runOrder}</td>
                          <td className="p-2">
                            {run.provider} · {run.modelUsed}
                          </td>
                          <td className="p-2 text-right">{run.totalTokens.toLocaleString()}</td>
                          <td className="p-2 text-right">{formatCurrency(run.estimatedCostUsd)}</td>
                          <td className="p-2 text-right">
                            <ClockIcon className="mr-1 inline h-3 w-3" />
                            {run.latencyMs}ms
                          </td>
                          <td className="p-2">
                            <Badge
                              variant="outline"
                              className={RUN_STATUS_BADGE_CLASS[run.status] ?? ''}
                            >
                              {MECHANIC_SECTION_RUN_STATUS_LABELS[run.status] ?? run.status}
                            </Badge>
                            {run.errorMessage && (
                              <span className="ml-1 text-rose-700" title={run.errorMessage}>
                                ⚠
                              </span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {lifecycleError && (
              <div className="rounded-md border border-rose-300 bg-rose-50 p-2 text-sm text-rose-800">
                <AlertTriangleIcon className="mr-1 inline h-4 w-4" />
                {lifecycleError}
              </div>
            )}

            {/* Lifecycle actions */}
            {status && (
              <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-4 dark:border-zinc-700">
                <Button
                  variant="outline"
                  onClick={() => submitReviewMutation.mutate()}
                  disabled={!canSubmitReview || submitReviewMutation.isPending}
                >
                  {submitReviewMutation.isPending ? (
                    <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
                  ) : (
                    <SendIcon className="mr-1 h-4 w-4" />
                  )}
                  Submit for review
                </Button>
                <Button
                  onClick={() => approveMutation.mutate()}
                  disabled={!canApprove || approveMutation.isPending}
                  className="bg-green-600 hover:bg-green-700 text-white"
                >
                  {approveMutation.isPending ? (
                    <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
                  ) : (
                    <CheckIcon className="mr-1 h-4 w-4" />
                  )}
                  Approve
                </Button>
                <Button
                  variant="outline"
                  onClick={() => {
                    setSuppressOpen(true);
                    setLifecycleError(null);
                  }}
                  disabled={!canSuppress}
                  className="border-rose-300 text-rose-700 hover:bg-rose-50"
                >
                  <ShieldAlertIcon className="mr-1 h-4 w-4" />
                  Suppress (T5)
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Suppress dialog */}
      <AlertDialog open={suppressOpen} onOpenChange={setSuppressOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Suppress this analysis?</AlertDialogTitle>
            <AlertDialogDescription>
              Applies the T5 kill-switch — the analysis will be hidden from player-facing queries.
              Suppression is orthogonal to the lifecycle (allowed from any status).
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="space-y-3">
            <div>
              <label className="mb-1 block text-sm font-medium">Reason (20–500 chars)</label>
              <textarea
                value={suppressReason}
                onChange={e => setSuppressReason(e.target.value)}
                placeholder="Legal takedown justification…"
                className="h-24 w-full resize-y rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-rose-400 focus:outline-none focus:ring-1 focus:ring-rose-400"
                data-testid="suppress-reason-input"
              />
              <p className="mt-1 text-xs text-muted-foreground">
                {suppressReason.length} / 20–500 characters
              </p>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Request source</label>
              <Select
                value={String(suppressSource)}
                onValueChange={v => setSuppressSource(Number(v) as SuppressionRequestSourceValue)}
              >
                <SelectTrigger data-testid="suppress-source-trigger">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(SUPPRESSION_REQUEST_SOURCE_LABELS).map(([value, label]) => (
                    <SelectItem key={value} value={value}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={suppressMutation.isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={e => {
                e.preventDefault();
                if (suppressReason.length < 20 || suppressReason.length > 500) return;
                suppressMutation.mutate();
              }}
              disabled={
                suppressReason.length < 20 ||
                suppressReason.length > 500 ||
                suppressMutation.isPending
              }
              className="bg-rose-600 hover:bg-rose-700"
              data-testid="suppress-confirm-button"
            >
              {suppressMutation.isPending ? (
                <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
              ) : null}
              Suppress
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
