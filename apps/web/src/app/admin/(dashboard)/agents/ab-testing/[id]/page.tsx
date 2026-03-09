'use client';

/**
 * A/B Test evaluation page — Blind comparison and scoring.
 * Issue #5502: A/B Testing frontend — Blind evaluation page.
 *
 * Flow: View blind responses → Score each variant (1-5) → Submit → Reveal models.
 */

import { use, useState, useCallback } from 'react';
// NOTE: AbTestEvaluationPageInner is exported for direct testing (avoids use(Promise) Suspense in jsdom)

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  FlaskConical,
  Loader2,
  Star,
  Send,
  Eye,
  BarChart3,
  AlertCircle,
  Trophy,
} from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { AbTestSessionRevealedDto } from '@/lib/api/schemas/ab-test.schemas';

import { AgentsNavConfig } from '../../NavConfig';

// Evaluation dimensions
const DIMENSIONS = ['accuracy', 'completeness', 'clarity', 'tone'] as const;
type Dimension = (typeof DIMENSIONS)[number];

const DIMENSION_LABELS: Record<Dimension, string> = {
  accuracy: 'Accuracy',
  completeness: 'Completeness',
  clarity: 'Clarity',
  tone: 'Tone',
};

interface VariantScores {
  accuracy: number;
  completeness: number;
  clarity: number;
  tone: number;
  notes: string;
}

function StarRating({
  value,
  onChange,
  disabled,
}: {
  value: number;
  onChange: (v: number) => void;
  disabled?: boolean;
}) {
  return (
    <div className="flex gap-1">
      {[1, 2, 3, 4, 5].map(star => (
        <button
          key={star}
          type="button"
          disabled={disabled}
          className={`transition-colors ${disabled ? 'cursor-default' : 'cursor-pointer hover:text-amber-400'}`}
          onClick={() => onChange(star)}
          aria-label={`${star} star${star > 1 ? 's' : ''}`}
        >
          <Star
            className={`h-5 w-5 ${
              star <= value
                ? 'fill-amber-400 text-amber-400'
                : 'fill-transparent text-slate-300 dark:text-zinc-600'
            }`}
          />
        </button>
      ))}
    </div>
  );
}

export function AbTestEvaluationPageInner({ id }: { id: string }) {
  const queryClient = useQueryClient();

  const { data: session, isLoading } = useQuery({
    queryKey: ['abTest', id],
    queryFn: () => api.admin.getAbTest(id),
  });

  const [scores, setScores] = useState<Record<string, VariantScores>>({});
  const [revealedData, setRevealedData] = useState<AbTestSessionRevealedDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const evaluateMutation = useMutation({
    mutationFn: () =>
      api.admin.evaluateAbTest(id, {
        evaluations: Object.entries(scores).map(([label, s]) => ({
          label,
          accuracy: s.accuracy,
          completeness: s.completeness,
          clarity: s.clarity,
          tone: s.tone,
          notes: s.notes || undefined,
        })),
      }),
    onSuccess: data => {
      setError(null);
      setRevealedData(data);
      queryClient.invalidateQueries({ queryKey: ['abTest', id] });
    },
    onError: err => {
      setError(err instanceof Error ? err.message : 'Evaluation failed');
    },
  });

  const updateScore = useCallback((label: string, dim: Dimension, value: number) => {
    setScores(prev => ({
      ...prev,
      [label]: {
        ...prev[label],
        [dim]: value,
      },
    }));
  }, []);

  const updateNotes = useCallback((label: string, notes: string) => {
    setScores(prev => ({
      ...prev,
      [label]: {
        ...prev[label],
        notes,
      },
    }));
  }, []);

  // Check if all non-failed variants have all dimensions scored
  const nonFailedVariants = session?.variants.filter(v => !v.failed) ?? [];
  const allScored = nonFailedVariants.every(v => {
    const s = scores[v.label];
    return s && DIMENSIONS.every(d => s[d] >= 1);
  });

  const isEvaluated = session?.status === 'Evaluated' || revealedData !== null;

  return (
    <div className="space-y-8">
      <AgentsNavConfig />

      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 dark:bg-amber-900/30">
            <FlaskConical className="h-5 w-5 text-amber-600 dark:text-amber-400" />
          </div>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              {isEvaluated ? 'A/B Test Results' : 'A/B Test Evaluation'}
            </h1>
            <p className="text-sm text-muted-foreground">
              {isEvaluated
                ? 'Models revealed — see comparison results'
                : 'Score each response blindly'}
            </p>
          </div>
        </div>
        {isEvaluated && (
          <Link href="/admin/agents/ab-testing/new">
            <Button variant="outline">
              <FlaskConical className="mr-2 h-4 w-4" />
              New Test
            </Button>
          </Link>
        )}
      </div>

      {/* Error */}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Loading */}
      {isLoading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
        </div>
      ) : !session ? (
        <p className="text-muted-foreground text-center py-20">A/B test session not found.</p>
      ) : (
        <>
          {/* Query */}
          <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-4 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
            <p className="text-sm text-muted-foreground">
              Query: <span className="font-medium text-foreground">{session.query}</span>
            </p>
            {isEvaluated && (
              <p className="text-xs text-muted-foreground mt-1">
                Total cost: ${session.totalCost.toFixed(6)}
              </p>
            )}
          </div>

          {/* Revealed Winner Banner */}
          {revealedData?.winnerLabel && (
            <div className="rounded-2xl border border-amber-300 bg-amber-50/80 p-4 dark:border-amber-700 dark:bg-amber-900/30">
              <div className="flex items-center gap-2">
                <Trophy className="h-5 w-5 text-amber-600" />
                <span className="font-semibold text-amber-800 dark:text-amber-200">
                  Winner: Variant {revealedData.winnerLabel}
                </span>
                <span className="text-sm text-amber-600 dark:text-amber-400">
                  ({revealedData.winnerModelId})
                </span>
              </div>
            </div>
          )}

          {/* Variants Grid */}
          <div
            className={`grid gap-6 ${
              session.variants.length <= 2 ? 'md:grid-cols-2' : 'md:grid-cols-2 xl:grid-cols-4'
            }`}
          >
            {session.variants.map(variant => {
              const revealed = revealedData?.variants.find(v => v.label === variant.label);
              const variantScores = scores[variant.label];

              return (
                <div
                  key={variant.id}
                  className={`rounded-2xl border p-5 transition-all ${
                    revealed
                      ? 'border-amber-300 bg-white/90 dark:border-amber-700 dark:bg-zinc-800/90'
                      : 'border-slate-200/60 bg-white/70 dark:border-zinc-700/40 dark:bg-zinc-800/70'
                  } backdrop-blur-md`}
                >
                  {/* Variant Header */}
                  <div className="mb-3 flex items-center justify-between">
                    <span className="font-quicksand text-lg font-bold text-amber-600 dark:text-amber-400">
                      Variant {variant.label}
                    </span>
                    <span className="text-xs text-muted-foreground">
                      {variant.latencyMs}ms · {variant.tokensUsed} tokens
                    </span>
                  </div>

                  {/* Revealed Model Info */}
                  {revealed && (
                    <div className="mb-3 flex items-center gap-2 rounded-lg bg-amber-50 p-2 dark:bg-amber-900/20">
                      <Eye className="h-4 w-4 text-amber-600" />
                      <span className="text-sm font-medium text-amber-800 dark:text-amber-200">
                        {revealed.modelId}
                      </span>
                      <span className="text-xs text-amber-600">({revealed.provider})</span>
                      {revealed.costUsd > 0 && (
                        <span className="ml-auto text-xs text-muted-foreground">
                          ${revealed.costUsd.toFixed(6)}
                        </span>
                      )}
                    </div>
                  )}

                  {/* Response */}
                  {variant.failed ? (
                    <div className="rounded-lg bg-red-50 p-3 dark:bg-red-900/20">
                      <p className="text-sm text-red-600 dark:text-red-400">
                        Failed: {variant.errorMessage ?? 'Unknown error'}
                      </p>
                    </div>
                  ) : (
                    <div className="mb-4 max-h-[300px] overflow-y-auto rounded-lg bg-slate-50 p-3 text-sm whitespace-pre-wrap dark:bg-zinc-900/50">
                      {variant.response}
                    </div>
                  )}

                  {/* Scoring (only for non-failed, non-evaluated) */}
                  {!variant.failed && !isEvaluated && (
                    <div className="space-y-3 border-t border-slate-200/60 pt-3 dark:border-zinc-700/40">
                      {DIMENSIONS.map(dim => (
                        <div key={dim} className="flex items-center justify-between">
                          <Label className="text-xs font-medium">{DIMENSION_LABELS[dim]}</Label>
                          <StarRating
                            value={variantScores?.[dim] ?? 0}
                            onChange={v => updateScore(variant.label, dim, v)}
                          />
                        </div>
                      ))}
                      <div>
                        <Label className="text-xs font-medium">Notes (optional)</Label>
                        <Textarea
                          placeholder="Optional feedback..."
                          className="mt-1 min-h-[60px] text-xs"
                          maxLength={2000}
                          value={variantScores?.notes ?? ''}
                          onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) =>
                            updateNotes(variant.label, e.target.value)
                          }
                        />
                      </div>
                    </div>
                  )}

                  {/* Evaluation Display (after submit) */}
                  {revealed?.evaluation && (
                    <div className="space-y-2 border-t border-slate-200/60 pt-3 dark:border-zinc-700/40">
                      {DIMENSIONS.map(dim => (
                        <div key={dim} className="flex items-center justify-between">
                          <span className="text-xs font-medium text-muted-foreground">
                            {DIMENSION_LABELS[dim]}
                          </span>
                          <StarRating
                            value={revealed.evaluation![dim]}
                            onChange={() => {}}
                            disabled
                          />
                        </div>
                      ))}
                      <p className="text-xs text-amber-600 font-medium">
                        Avg: {revealed.evaluation.averageScore.toFixed(1)}
                      </p>
                      {revealed.evaluation.notes && (
                        <p className="text-xs text-muted-foreground italic">
                          {revealed.evaluation.notes}
                        </p>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {/* Actions */}
          {!isEvaluated && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                {allScored
                  ? 'All variants scored. Ready to submit!'
                  : 'Score all variants to submit evaluation.'}
              </p>
              <Button
                size="lg"
                className="bg-amber-500 hover:bg-amber-600 text-white px-8"
                disabled={!allScored || evaluateMutation.isPending}
                onClick={() => evaluateMutation.mutate()}
              >
                {evaluateMutation.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                    Evaluating...
                  </>
                ) : (
                  <>
                    <Send className="mr-2 h-5 w-5" />
                    Submit Evaluation
                  </>
                )}
              </Button>
            </div>
          )}

          {/* Post-evaluation links */}
          {isEvaluated && (
            <div className="flex justify-center gap-4">
              <Link href="/admin/agents/ab-testing/new">
                <Button variant="outline">
                  <FlaskConical className="mr-2 h-4 w-4" />
                  Run Another Test
                </Button>
              </Link>
              <Link href="/admin/agents/ab-testing/results">
                <Button variant="outline">
                  <BarChart3 className="mr-2 h-4 w-4" />
                  View Analytics
                </Button>
              </Link>
            </div>
          )}
        </>
      )}
    </div>
  );
}

export default function AbTestEvaluationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <AbTestEvaluationPageInner id={id} />;
}
