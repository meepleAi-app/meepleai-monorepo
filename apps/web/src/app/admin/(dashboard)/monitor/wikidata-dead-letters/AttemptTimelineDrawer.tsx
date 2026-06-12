/**
 * Issue #1823 Phase E F3 — per-game WikidataCoverEnrichmentAttempt timeline
 * surfaced as a slide-over drawer on the admin dead-letter visibility page.
 *
 * Each node maps 1:1 to an attempt row (Success / Skipped / Failed-with-retry /
 * DeadLetter) so operators can reconstruct the full enrichment history without
 * leaving the page.
 */

'use client';

import { useEffect, useState } from 'react';

import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import {
  getAttemptTimeline,
  type AttemptTimelineOutcome,
  type WikidataAttemptTimelineNode,
} from '@/lib/api/admin-wikidata-dead-letters';

interface AttemptTimelineDrawerProps {
  readonly gameId: string;
  readonly gameTitle: string;
  readonly open: boolean;
  readonly onClose: () => void;
}

const OUTCOME_TONE: Record<AttemptTimelineOutcome, string> = {
  Success: 'border-emerald-500 bg-emerald-500/10',
  Skipped: 'border-slate-500 bg-slate-500/10',
  Failed: 'border-amber-500 bg-amber-500/10',
  DeadLetter: 'border-destructive bg-destructive/10',
};

function formatUtc(iso: string): string {
  return new Date(iso).toISOString().slice(0, 19).replace('T', ' ');
}

export function AttemptTimelineDrawer({
  gameId,
  gameTitle,
  open,
  onClose,
}: AttemptTimelineDrawerProps): React.JSX.Element {
  const [items, setItems] = useState<WikidataAttemptTimelineNode[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getAttemptTimeline(gameId, 50)
      .then(result => {
        if (cancelled) return;
        setItems(result.items);
      })
      .catch(err => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Unknown error');
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [gameId, open]);

  return (
    <Drawer open={open} onOpenChange={next => (next ? null : onClose())} direction="right">
      <DrawerContent className="flex max-h-screen w-full max-w-xl flex-col">
        <DrawerHeader>
          <DrawerTitle>Attempt timeline — {gameTitle}</DrawerTitle>
          <DrawerDescription>
            Most recent attempt at the top. Rows are bounded at 50; older history is pruned by the
            7-day retention sweep.
          </DrawerDescription>
        </DrawerHeader>

        <div className="flex-1 overflow-y-auto p-4">
          {loading && <p className="text-sm text-muted-foreground">Loading timeline…</p>}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          {!loading && !error && items.length === 0 && (
            <p className="text-sm text-muted-foreground">No attempt history for this game.</p>
          )}
          {!loading && !error && items.length > 0 && (
            <ol className="space-y-3" aria-label="Attempt history">
              {items.map(node => (
                <li
                  key={node.id}
                  className={`rounded-l-md border-l-4 p-3 ${OUTCOME_TONE[node.outcome]}`}
                  data-outcome={node.outcome}
                >
                  <div className="flex items-baseline justify-between gap-3">
                    <span className="text-sm font-semibold">{node.outcome}</span>
                    <time className="text-xs text-muted-foreground" dateTime={node.attemptedAt}>
                      {formatUtc(node.attemptedAt)}
                    </time>
                  </div>
                  {node.reason && (
                    <p className="mt-1 text-xs">
                      <span className="text-muted-foreground">reason: </span>
                      <code className="rounded bg-muted px-1 py-0.5 text-xs">{node.reason}</code>
                    </p>
                  )}
                  {node.details && (
                    <p className="mt-1 text-xs text-muted-foreground">{node.details}</p>
                  )}
                  <p className="mt-1 text-xs text-muted-foreground">
                    retries: <span className="font-medium text-foreground">{node.retryCount}</span>
                    {node.nextRetryAt && (
                      <>
                        {' · '}next retry: <time>{formatUtc(node.nextRetryAt)}</time>
                      </>
                    )}
                    {node.deadLetteredAt && (
                      <>
                        {' · '}dead-lettered: <time>{formatUtc(node.deadLetteredAt)}</time>
                      </>
                    )}
                  </p>
                </li>
              ))}
            </ol>
          )}
        </div>
      </DrawerContent>
    </Drawer>
  );
}
