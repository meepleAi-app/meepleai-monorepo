'use client';

import { useState } from 'react';

import { AlertCircle, CheckCircle2, Loader2, PlayCircle, Sparkles } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

import {
  useEnqueueAllSkeletons,
  useEnqueueEnrichment,
  useMarkComplete,
  type EnqueueResult,
} from '../lib/catalog-ingestion-api';

export function EnrichmentQueueTab() {
  const [gameIdsInput, setGameIdsInput] = useState('');
  const enqueueAll = useEnqueueAllSkeletons();
  const enqueueSelected = useEnqueueEnrichment();
  const markComplete = useMarkComplete();

  const parsedIds = gameIdsInput
    .split(',')
    .map(id => id.trim())
    .filter(Boolean);

  const handleEnqueueSelected = () => {
    if (parsedIds.length === 0) return;
    enqueueSelected.mutate(parsedIds);
  };

  const handleMarkComplete = () => {
    if (parsedIds.length === 0) return;
    markComplete.mutate(parsedIds);
  };

  const lastEnqueueResult = (enqueueAll.data ?? enqueueSelected.data) as EnqueueResult | undefined;
  const lastCompleteResult = markComplete.data as { completed: number } | undefined;

  const anyError = enqueueAll.error ?? enqueueSelected.error ?? markComplete.error;
  const anyPending = enqueueAll.isPending || enqueueSelected.isPending || markComplete.isPending;

  return (
    <div className="space-y-6">
      {/* Enqueue All Skeletons */}
      <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6 space-y-4">
        <h3 className="font-quicksand text-lg font-semibold text-foreground">
          Enqueue All Skeletons
        </h3>
        <p className="text-sm text-muted-foreground">
          Queue all skeleton games (status = Skeleton) for enrichment. Games that already have an
          enrichment job pending will be skipped.
        </p>
        <Button onClick={() => enqueueAll.mutate()} disabled={anyPending} size="sm">
          {enqueueAll.isPending ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Sparkles className="h-4 w-4" />
          )}
          Enqueue All Skeletons
        </Button>
      </div>

      {/* Selective operations */}
      <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6 space-y-4">
        <h3 className="font-quicksand text-lg font-semibold text-foreground">
          Selective Operations
        </h3>
        <p className="text-sm text-muted-foreground">
          Enter comma-separated shared game IDs to enqueue for enrichment or mark as complete.
        </p>

        <div className="space-y-2">
          <Label htmlFor="game-ids">Game IDs</Label>
          <Input
            id="game-ids"
            placeholder="e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6, ..."
            value={gameIdsInput}
            onChange={e => setGameIdsInput(e.target.value)}
          />
          {parsedIds.length > 0 && (
            <p className="text-xs text-muted-foreground">
              {parsedIds.length} ID{parsedIds.length !== 1 ? 's' : ''} entered
            </p>
          )}
        </div>

        <div className="flex items-center gap-3">
          <Button
            onClick={handleEnqueueSelected}
            disabled={parsedIds.length === 0 || anyPending}
            size="sm"
            variant="secondary"
          >
            {enqueueSelected.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <PlayCircle className="h-4 w-4" />
            )}
            Enqueue Selected
          </Button>
          <Button
            onClick={handleMarkComplete}
            disabled={parsedIds.length === 0 || anyPending}
            size="sm"
            variant="outline"
          >
            {markComplete.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <CheckCircle2 className="h-4 w-4" />
            )}
            Mark Complete
          </Button>
        </div>
      </div>

      {/* Error */}
      {anyError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {anyError instanceof Error ? anyError.message : 'Operation failed.'}
          </AlertDescription>
        </Alert>
      )}

      {/* Results */}
      {lastEnqueueResult && (
        <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
          <div className="flex items-center gap-2 mb-3">
            <CheckCircle2 className="h-5 w-5 text-emerald-500" />
            <span className="font-quicksand font-semibold text-foreground">Enqueue Result</span>
          </div>
          <p className="text-sm text-muted-foreground">
            <span className="font-medium text-foreground">{lastEnqueueResult.enqueued}</span>{' '}
            enqueued,{' '}
            <span className="font-medium text-foreground">{lastEnqueueResult.skipped}</span> skipped
          </p>
        </div>
      )}

      {lastCompleteResult && (
        <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
          <div className="flex items-center gap-2 mb-3">
            <CheckCircle2 className="h-5 w-5 text-emerald-500" />
            <span className="font-quicksand font-semibold text-foreground">
              Mark Complete Result
            </span>
          </div>
          <p className="text-sm text-muted-foreground">
            <span className="font-medium text-foreground">{lastCompleteResult.completed}</span>{' '}
            games marked as complete
          </p>
        </div>
      )}
    </div>
  );
}
