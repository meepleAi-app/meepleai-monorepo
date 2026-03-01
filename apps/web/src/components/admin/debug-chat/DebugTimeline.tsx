'use client';

/**
 * DebugTimeline - Vertical timeline of debug events
 *
 * Renders a scrollable list of DebugEventCards, auto-scrolls to latest,
 * and shows a running cost display at the top.
 */

import { useRef, useEffect } from 'react';
import { DebugEventCard } from './DebugEventCard';
import { DebugCostBadge } from './DebugCostBadge';
import type { DebugEvent } from '@/hooks/useDebugChatStream';

interface DebugTimelineProps {
  events: DebugEvent[];
  isStreaming: boolean;
}

export function DebugTimeline({ events, isStreaming }: DebugTimelineProps) {
  const scrollRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom when new events arrive
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [events.length]);

  // Extract cost data from the most recent DebugCostUpdate event (type 17)
  const costEvent = events.findLast(e => e.type === 17);
  const costData = costEvent?.data as {
    promptTokens?: number;
    completionTokens?: number;
    totalTokens?: number;
    costUsd?: number;
    modelId?: string;
  } | undefined;

  return (
    <div className="flex h-full flex-col">
      {/* Header with cost display */}
      <div className="flex items-center justify-between border-b px-4 py-2">
        <h3 className="text-sm font-semibold text-foreground">Pipeline Debug</h3>
        <div className="flex items-center gap-2">
          {costData && (
            <DebugCostBadge
              totalTokens={costData.totalTokens ?? 0}
              costUsd={costData.costUsd}
              modelId={costData.modelId}
            />
          )}
          {isStreaming && (
            <span className="inline-flex items-center gap-1.5 text-xs text-amber-500">
              <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" />
              Live
            </span>
          )}
        </div>
      </div>

      {/* Events list */}
      <div
        ref={scrollRef}
        className="flex-1 overflow-y-auto px-3 py-2 space-y-1.5"
      >
        {events.length === 0 ? (
          <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
            Send a message to see pipeline events
          </div>
        ) : (
          events.map(event => (
            <DebugEventCard key={event.id} event={event} />
          ))
        )}
      </div>

      {/* Footer stats */}
      {events.length > 0 && (
        <div className="border-t px-4 py-1.5 text-xs text-muted-foreground">
          {events.length} event{events.length !== 1 ? 's' : ''}
          {events.length > 0 && (
            <span className="ml-2">
              &middot; {Math.round(events[events.length - 1].elapsedMs)}ms total
            </span>
          )}
        </div>
      )}
    </div>
  );
}
