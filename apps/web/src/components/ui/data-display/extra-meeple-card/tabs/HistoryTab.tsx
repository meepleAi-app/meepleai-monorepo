'use client';

/**
 * HistoryTab - Session snapshots and timeline
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 */

import React from 'react';

import {
  Camera,
  ChevronRight,
  Clock,
  History,
  Zap,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import type { HistoryTabData, SnapshotInfo, SessionTimelineEvent } from '../types';

// ============================================================================
// Sub-components
// ============================================================================

const TRIGGER_ICONS: Record<string, React.ElementType> = {
  manual: Camera,
  turnEnd: ChevronRight,
  automatic: Zap,
};

function SnapshotCard({ snapshot }: { snapshot: SnapshotInfo }) {
  const Icon = TRIGGER_ICONS[snapshot.triggerType] ?? Camera;
  const time = new Date(snapshot.createdAt);
  const formattedTime = time.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div className="flex items-start gap-2.5 rounded-lg bg-white/60 px-3 py-2">
      <div className="mt-0.5 flex h-6 w-6 items-center justify-center rounded-full bg-indigo-100 flex-shrink-0">
        <Icon className="h-3 w-3 text-indigo-600" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline gap-2">
          <span className="text-xs font-bold text-slate-700 font-nunito">
            #{snapshot.snapshotNumber}
          </span>
          {snapshot.turnNumber != null && (
            <span className="text-[10px] text-slate-400">Turn {snapshot.turnNumber}</span>
          )}
        </div>
        <p className="text-xs text-slate-600 font-nunito line-clamp-1">
          {snapshot.description}
        </p>
      </div>
      <span className="text-[10px] text-slate-400 tabular-nums flex-shrink-0">
        {formattedTime}
      </span>
    </div>
  );
}

const EVENT_TYPE_STYLES: Record<string, { dot: string; text: string }> = {
  turn: { dot: 'bg-blue-400', text: 'text-blue-600' },
  score: { dot: 'bg-amber-400', text: 'text-amber-600' },
  action: { dot: 'bg-green-400', text: 'text-green-600' },
  media: { dot: 'bg-purple-400', text: 'text-purple-600' },
  system: { dot: 'bg-slate-400', text: 'text-slate-500' },
};

function TimelineItem({ event }: { event: SessionTimelineEvent }) {
  const style = EVENT_TYPE_STYLES[event.type] ?? EVENT_TYPE_STYLES.system;
  const time = new Date(event.timestamp);
  const formattedTime = time.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });

  return (
    <div className="flex items-start gap-2.5 py-1.5">
      {/* Timeline dot + line */}
      <div className="flex flex-col items-center pt-1">
        <div className={cn('h-2 w-2 rounded-full flex-shrink-0', style.dot)} />
      </div>
      <div className="min-w-0 flex-1">
        <p className={cn('text-xs font-medium font-nunito', style.text)}>
          {event.label}
        </p>
      </div>
      <span className="text-[10px] text-slate-400 tabular-nums flex-shrink-0">
        {formattedTime}
      </span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

interface HistoryTabProps {
  data?: HistoryTabData;
}

export function HistoryTab({ data }: HistoryTabProps) {
  if (!data || (data.snapshots.length === 0 && data.timeline.length === 0)) {
    return (
      <div className="flex h-48 flex-col items-center justify-center gap-2 text-slate-400">
        <History className="h-8 w-8 opacity-30" />
        <span className="font-nunito text-sm">No history yet</span>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Snapshots */}
      {data.snapshots.length > 0 && (
        <div>
          <div className="mb-2 flex items-center gap-1.5">
            <Camera className="h-3.5 w-3.5 text-indigo-500" />
            <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
              Snapshots ({data.snapshots.length})
            </h3>
          </div>
          <div className="space-y-1.5">
            {data.snapshots.map((snapshot) => (
              <SnapshotCard key={snapshot.id} snapshot={snapshot} />
            ))}
          </div>
        </div>
      )}

      {/* Timeline */}
      {data.timeline.length > 0 && (
        <div className={data.snapshots.length > 0 ? 'border-t border-slate-100 pt-3' : ''}>
          <div className="mb-2 flex items-center gap-1.5">
            <Clock className="h-3.5 w-3.5 text-indigo-500" />
            <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
              Timeline ({data.timeline.length})
            </h3>
          </div>
          <div className="divide-y divide-slate-50">
            {data.timeline.map((event) => (
              <TimelineItem key={event.id} event={event} />
            ))}
          </div>
        </div>
      )}

      {/* Summary */}
      {data.totalTurns != null && (
        <div className="rounded-lg bg-indigo-50/40 px-3 py-2 text-xs text-indigo-600 font-nunito">
          Total turns: {data.totalTurns}
        </div>
      )}
    </div>
  );
}
