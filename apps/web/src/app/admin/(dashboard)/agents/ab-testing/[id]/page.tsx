'use client';

/**
 * A/B Test evaluation page — Blind comparison view.
 * Issue #5502: A/B Testing frontend — Blind evaluation page.
 * Placeholder — full implementation in D5/#5502.
 */

import { use } from 'react';

import { useQuery } from '@tanstack/react-query';
import { FlaskConical, Loader2 } from 'lucide-react';

import { api } from '@/lib/api';

import { AgentsNavConfig } from '../../NavConfig';

export default function AbTestEvaluationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  const { data: session, isLoading } = useQuery({
    queryKey: ['abTest', id],
    queryFn: () => api.admin.getAbTest(id),
  });

  return (
    <div className="space-y-8">
      <AgentsNavConfig />

      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 dark:bg-amber-900/30">
          <FlaskConical className="h-5 w-5 text-amber-600 dark:text-amber-400" />
        </div>
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            A/B Test Evaluation
          </h1>
          <p className="text-sm text-muted-foreground">
            Compare responses blindly, then score each variant
          </p>
        </div>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
        </div>
      ) : session ? (
        <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-6 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
          <p className="text-sm text-muted-foreground mb-4">
            Query: <span className="font-medium text-foreground">{session.query}</span>
          </p>
          <div className="grid gap-4 md:grid-cols-2">
            {session.variants.map(variant => (
              <div
                key={variant.id}
                className="rounded-xl border border-slate-200/60 p-4 dark:border-zinc-700/40"
              >
                <div className="mb-2 flex items-center justify-between">
                  <span className="font-quicksand text-lg font-bold text-amber-600">
                    Variant {variant.label}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {variant.latencyMs}ms · {variant.tokensUsed} tokens
                  </span>
                </div>
                {variant.failed ? (
                  <p className="text-sm text-red-500">
                    Failed: {variant.errorMessage ?? 'Unknown error'}
                  </p>
                ) : (
                  <p className="text-sm whitespace-pre-wrap">{variant.response}</p>
                )}
              </div>
            ))}
          </div>
          <p className="mt-4 text-xs text-muted-foreground text-center">
            Full evaluation UI coming in Issue #5502
          </p>
        </div>
      ) : (
        <p className="text-muted-foreground text-center py-20">A/B test session not found.</p>
      )}
    </div>
  );
}
