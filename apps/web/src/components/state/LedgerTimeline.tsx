/**
 * LedgerTimeline Component
 * Issue #2422: Ledger Mode History Timeline
 *
 * Main timeline orchestrator with diff visualization, rollback, and export.
 * Sub-Issue of #2406: Game State Editor UI - Sprint 4
 */

'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Separator } from '@/components/ui/navigation/separator';
import { useGameStateStore } from '@/lib/stores/game-state-store';

import { LedgerTimelineExport } from './LedgerTimelineExport';
import { LedgerTimelineItem } from './LedgerTimelineItem';
import { useRollback } from './LedgerTimelineRollback';

interface LedgerTimelineProps {
  sessionId: string;
}

export function LedgerTimeline({ sessionId }: LedgerTimelineProps) {
  const { snapshots, loadSnapshot } = useGameStateStore();

  const { initiateRollback, RollbackDialog } = useRollback({
    onConfirm: (snapshotId: string) => {
      loadSnapshot(snapshotId);
    },
  });

  const reversedSnapshots = [...snapshots].reverse();

  // Empty state
  if (snapshots.length === 0) {
    return (
      <Card data-testid="ledger-timeline-empty">
        <CardHeader>
          <CardTitle>Ledger History</CardTitle>
          <CardDescription>No history available yet</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            Actions will appear here as the game progresses
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <>
      <Card data-testid="ledger-timeline">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Ledger History</CardTitle>
              <CardDescription>{snapshots.length} actions recorded</CardDescription>
            </div>
            <LedgerTimelineExport snapshots={snapshots} sessionId={sessionId} />
          </div>
        </CardHeader>
        <CardContent>
          <ScrollArea className="h-[500px] pr-4">
            <div className="space-y-2">
              {reversedSnapshots.map((snapshot, index) => {
                const previousSnapshot = reversedSnapshots[index + 1];
                const isLatest = index === 0;

                return (
                  <div key={snapshot.id}>
                    <LedgerTimelineItem
                      snapshot={snapshot}
                      previousSnapshot={previousSnapshot}
                      isLatest={isLatest}
                      onRestore={initiateRollback}
                    />
                    {index < reversedSnapshots.length - 1 && <Separator className="my-2" />}
                  </div>
                );
              })}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>

      <RollbackDialog />
    </>
  );
}
