/**
 * StateHistoryTimeline Component
 * Issue #2406: Game State Editor UI
 *
 * Timeline displaying state history snapshots with navigation.
 */

'use client';

import { Clock, RotateCcw } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Separator } from '@/components/ui/navigation/separator';
import { useGameStateStore } from '@/lib/stores/game-state-store';

interface StateHistoryTimelineProps {
  sessionId: string;
}

export function StateHistoryTimeline({ sessionId: _sessionId }: StateHistoryTimelineProps) {
  const { snapshots, loadSnapshot } = useGameStateStore();

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    }).format(date);
  };

  const handleLoadSnapshot = (snapshotId: string) => {
    if (confirm('Load this snapshot? Current unsaved changes will be lost.')) {
      loadSnapshot(snapshotId);
    }
  };

  if (snapshots.length === 0) {
    return (
      <Card data-testid="state-history-timeline">
        <CardHeader className="pb-3 sm:pb-6">
          <CardTitle className="text-base sm:text-lg">State History</CardTitle>
          <CardDescription className="text-xs sm:text-sm">
            No snapshots available yet
          </CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-xs sm:text-sm text-muted-foreground">
            Snapshots will appear here as the game progresses
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card data-testid="state-history-timeline">
      <CardHeader className="pb-3 sm:pb-6">
        <CardTitle className="text-base sm:text-lg">State History</CardTitle>
        <CardDescription className="text-xs sm:text-sm">
          {snapshots.length} snapshots available
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ScrollArea className="h-[300px] sm:h-[350px] md:h-[400px] pr-2 sm:pr-4">
          <div className="space-y-3 sm:space-y-4">
            {snapshots
              .slice()
              .reverse()
              .map((snapshot, index) => (
                <div key={snapshot.id}>
                  <div className="flex items-start gap-4">
                    {/* Timeline dot */}
                    <div className="flex flex-col items-center">
                      <div
                        className={`h-3 w-3 rounded-full ${
                          index === 0 ? 'bg-primary' : 'bg-muted-foreground'
                        }`}
                        aria-hidden="true"
                      />
                      {index < snapshots.length - 1 && (
                        <div className="w-0.5 h-16 bg-muted" aria-hidden="true" />
                      )}
                    </div>

                    {/* Snapshot info */}
                    <div className="flex-1 space-y-2 pb-4">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <Clock className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm font-medium">
                            {formatTimestamp(snapshot.timestamp)}
                          </span>
                          {index === 0 && (
                            <Badge variant="default" className="text-xs">
                              Latest
                            </Badge>
                          )}
                        </div>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleLoadSnapshot(snapshot.id)}
                          disabled={index === 0}
                        >
                          <RotateCcw className="mr-2 h-3 w-3" />
                          Restore
                        </Button>
                      </div>

                      <p className="text-sm text-muted-foreground">{snapshot.action}</p>

                      {/* Preview key stats */}
                      <div className="flex gap-3 text-xs text-muted-foreground">
                        {snapshot.state.roundNumber !== undefined && (
                          <span>Round: {snapshot.state.roundNumber}</span>
                        )}
                        {snapshot.state.phase && <span>Phase: {snapshot.state.phase}</span>}
                        <span>Players: {snapshot.state.players.length}</span>
                      </div>
                    </div>
                  </div>

                  {index < snapshots.length - 1 && <Separator className="my-2" />}
                </div>
              ))}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
