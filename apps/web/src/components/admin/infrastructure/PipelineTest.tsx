'use client';

import { CheckCircle, Loader2, XCircle, Zap } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { usePipelineTest } from '@/hooks/admin/use-infrastructure';
import type { PipelineHop } from '@/lib/api/clients/infrastructureClient';

function HopChain({ hops }: { hops: PipelineHop[] }) {
  return (
    <div className="flex flex-wrap items-center gap-1 text-sm">
      {hops.map((hop, i) => {
        const ok = hop.status === 'Healthy' || hop.status === 'ok';
        return (
          <span key={hop.serviceName} className="flex items-center gap-1">
            {i > 0 && <span className="text-muted-foreground mx-1">&rarr;</span>}
            {ok ? (
              <CheckCircle className="h-3.5 w-3.5 text-green-500" />
            ) : (
              <XCircle className="h-3.5 w-3.5 text-red-500" />
            )}
            <span className={ok ? '' : 'text-red-600 dark:text-red-400'}>{hop.displayName}</span>
            <span className="text-xs text-muted-foreground">({hop.latencyMs}ms)</span>
          </span>
        );
      })}
    </div>
  );
}

export function PipelineTest() {
  const { mutate, data, isPending } = usePipelineTest();

  return (
    <div className="rounded-lg border border-slate-200/60 dark:border-zinc-700/60 bg-white/90 dark:bg-zinc-800/90 p-4 space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Zap className="h-4 w-4 text-amber-500" />
          <span className="text-sm font-medium">Pipeline Connectivity</span>
        </div>
        <Button variant="outline" size="sm" onClick={() => mutate()} disabled={isPending}>
          {isPending ? (
            <>
              <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
              Testing...
            </>
          ) : (
            'Test Pipeline'
          )}
        </Button>
      </div>

      {data && (
        <div className="space-y-2">
          <HopChain hops={data.hops} />
          <p className="text-xs text-muted-foreground">
            {data.success ? (
              <span className="text-green-600 dark:text-green-400 font-medium">
                Pipeline OK &mdash; Total: {data.totalLatencyMs}ms
              </span>
            ) : (
              <span className="text-red-600 dark:text-red-400 font-medium">
                Pipeline broken &mdash; check failing services above
              </span>
            )}
          </p>
        </div>
      )}
    </div>
  );
}
