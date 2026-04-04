'use client';

import { useState, useCallback } from 'react';

import { Loader2, Sparkles } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useApiClient } from '@/lib/api/context';
import type { TurnSummaryResult } from '@/lib/api/schemas/session-tracking.schemas';

export interface TurnSummaryButtonProps {
  sessionId: string;
  lastNEvents?: number;
  fromPhase?: number;
  toPhase?: number;
  disabled?: boolean;
  className?: string;
}

export function TurnSummaryButton({
  sessionId,
  lastNEvents = 20,
  fromPhase,
  toPhase,
  disabled = false,
  className,
}: TurnSummaryButtonProps) {
  const { sessionTracking } = useApiClient();
  const [isLoading, setIsLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [summary, setSummary] = useState<TurnSummaryResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleGenerateSummary = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    setSummary(null);

    try {
      const result = await sessionTracking.getTurnSummary(sessionId, {
        lastNEvents: fromPhase !== undefined || toPhase !== undefined ? undefined : lastNEvents,
        fromPhase,
        toPhase,
      });
      setSummary(result);
      setIsOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to generate summary');
    } finally {
      setIsLoading(false);
    }
  }, [sessionTracking, sessionId, lastNEvents, fromPhase, toPhase]);

  return (
    <>
      <Button
        variant="outline"
        size="sm"
        onClick={handleGenerateSummary}
        disabled={disabled || isLoading}
        className={className}
        aria-label="Generate AI turn summary"
      >
        {isLoading ? (
          <>
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Generating...
          </>
        ) : (
          <>
            <Sparkles className="mr-2 h-4 w-4" />
            Summarize Turn
          </>
        )}
      </Button>

      {error && (
        <div role="alert" className="mt-2 text-sm text-destructive">
          {error}
        </div>
      )}

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Turn Summary</DialogTitle>
            <DialogDescription>
              AI analysis of {summary?.eventsAnalyzed ?? 0} events
            </DialogDescription>
          </DialogHeader>
          <div className="mt-4 whitespace-pre-wrap text-sm leading-relaxed">{summary?.summary}</div>
        </DialogContent>
      </Dialog>
    </>
  );
}
