'use client';

/**
 * SnapshotCard — Displays a single session snapshot with images and status.
 *
 * Session Vision AI — Task 15.2
 */

import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';
import { cn } from '@/lib/utils';

interface SnapshotCardProps {
  snapshot: SessionSnapshotDto;
  isLatest?: boolean;
  className?: string;
}

export function SnapshotCard({ snapshot, isLatest, className }: SnapshotCardProps) {
  const date = new Date(snapshot.createdAt);
  const timeLabel = date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });

  return (
    <div
      className={cn(
        'rounded-xl border bg-[var(--nh-bg-elevated)] p-3 shadow-sm transition-all',
        isLatest ? 'border-amber-300 ring-1 ring-amber-200' : 'border-[var(--nh-border-default)]',
        className
      )}
    >
      {/* Header row */}
      <div className="mb-2 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="font-quicksand text-sm font-bold text-[var(--nh-text-primary)]">
            Turno {snapshot.turnNumber}
          </span>
          {isLatest && (
            <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold text-amber-700">
              ULTIMO
            </span>
          )}
        </div>

        {/* Status badge */}
        <span
          className={cn(
            'rounded-full px-2 py-0.5 text-[10px] font-semibold',
            snapshot.hasGameState ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700'
          )}
        >
          {snapshot.hasGameState ? 'Analizzato' : 'Non analizzato'}
        </span>
      </div>

      {/* Image thumbnails */}
      {snapshot.images.length > 0 && (
        <div className="mb-2 flex gap-2 overflow-x-auto">
          {snapshot.images
            .sort((a, b) => a.orderIndex - b.orderIndex)
            .map(img => (
              <a
                key={img.id}
                href={img.downloadUrl ?? '#'}
                target="_blank"
                rel="noopener noreferrer"
                className="block flex-shrink-0"
              >
                <img
                  src={img.downloadUrl ?? ''}
                  alt={`Snapshot turno ${snapshot.turnNumber}`}
                  className="h-16 w-20 rounded-lg border border-[var(--nh-border-default)] object-cover transition-opacity hover:opacity-80"
                />
              </a>
            ))}
        </div>
      )}

      {/* Caption + timestamp */}
      <div className="flex items-end justify-between gap-2">
        {snapshot.caption && (
          <p className="flex-1 font-nunito text-xs text-[var(--nh-text-secondary)] line-clamp-2">
            {snapshot.caption}
          </p>
        )}
        <span className="shrink-0 font-mono text-[10px] text-[var(--nh-text-muted)]">
          {timeLabel}
        </span>
      </div>
    </div>
  );
}
