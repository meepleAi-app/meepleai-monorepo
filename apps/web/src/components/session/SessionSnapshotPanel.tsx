'use client';

/**
 * SessionSnapshotPanel — Main panel for session snapshots with upload.
 *
 * Session Vision AI — Task 15.4
 *
 * Renders snapshot list, game state, upload dialog, and empty state.
 */

import { useState } from 'react';

import { Camera, ImageOff } from 'lucide-react';

import {
  useSessionVisionSnapshots,
  useLatestGameState,
  useCreateSnapshot,
} from '@/hooks/queries/useSessionSnapshots';
import { cn } from '@/lib/utils';

import { GameStateDisplay } from './GameStateDisplay';
import { SnapshotCard } from './SnapshotCard';
import { SnapshotUploadDialog } from './SnapshotUploadDialog';

interface SessionSnapshotPanelProps {
  sessionId: string;
  userId: string;
  currentTurn?: number;
  className?: string;
}

export function SessionSnapshotPanel({
  sessionId,
  userId,
  currentTurn = 1,
  className,
}: SessionSnapshotPanelProps) {
  const [dialogOpen, setDialogOpen] = useState(false);

  const { data: snapshots, isLoading } = useSessionVisionSnapshots(sessionId);
  const { data: gameState } = useLatestGameState(sessionId);
  const createMutation = useCreateSnapshot(sessionId);

  const handleUpload = (images: File[], caption: string, turnNumber: number) => {
    createMutation.mutate(
      { userId, turnNumber, images, caption: caption || undefined },
      { onSuccess: () => setDialogOpen(false) }
    );
  };

  const snapshotList = snapshots ?? [];

  return (
    <div className={cn('space-y-4', className)}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="flex items-center gap-2 font-quicksand text-lg font-bold text-[var(--nh-text-primary)]">
            <Camera className="h-5 w-5 text-amber-500" />
            Stato Partita
          </h2>
          <p className="mt-0.5 font-nunito text-xs text-[var(--nh-text-muted)]">
            {snapshotList.length === 0
              ? 'Nessuno snapshot ancora'
              : `${snapshotList.length} snapshot`}
          </p>
        </div>

        <button
          type="button"
          onClick={() => setDialogOpen(true)}
          className="flex items-center gap-1.5 rounded-xl bg-amber-500 px-3 py-2 font-nunito text-xs font-semibold text-white shadow-sm transition-colors hover:bg-amber-600"
        >
          + Nuovo Snapshot
        </button>
      </div>

      {/* Latest game state */}
      {gameState?.gameStateJson && <GameStateDisplay gameStateJson={gameState.gameStateJson} />}

      {/* Loading */}
      {isLoading && (
        <p className="py-6 text-center font-nunito text-sm text-[var(--nh-text-muted)]">
          Caricamento snapshot...
        </p>
      )}

      {/* Empty state */}
      {!isLoading && snapshotList.length === 0 && (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-stone-300 py-10">
          <ImageOff className="h-10 w-10 text-stone-300" />
          <p className="font-nunito text-sm text-[var(--nh-text-muted)]">
            Scatta una foto del tavolo per analizzare lo stato della partita
          </p>
          <button
            type="button"
            onClick={() => setDialogOpen(true)}
            className="rounded-lg border border-[var(--nh-border-default)] bg-white px-4 py-2 font-nunito text-xs font-medium text-[var(--nh-text-primary)] transition-colors hover:bg-stone-50"
          >
            <Camera className="mr-1.5 inline-block h-3.5 w-3.5" />
            Primo snapshot
          </button>
        </div>
      )}

      {/* Snapshot list */}
      {snapshotList.length > 0 && (
        <div className="space-y-3">
          {snapshotList
            .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
            .map((snap, idx) => (
              <SnapshotCard key={snap.id} snapshot={snap} isLatest={idx === 0} />
            ))}
        </div>
      )}

      {/* Upload dialog */}
      <SnapshotUploadDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onUpload={handleUpload}
        defaultTurnNumber={currentTurn}
        isUploading={createMutation.isPending}
      />
    </div>
  );
}
