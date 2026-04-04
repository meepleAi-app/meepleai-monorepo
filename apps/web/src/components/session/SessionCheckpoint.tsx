'use client';

import { useState, useCallback } from 'react';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { api } from '@/lib/api';
import type { SessionCheckpointDto } from '@/lib/api/schemas/session-tracking.schemas';

interface SessionCheckpointProps {
  sessionId: string;
}

export function SessionCheckpoint({ sessionId }: SessionCheckpointProps) {
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [listDialogOpen, setListDialogOpen] = useState(false);
  const [restoreConfirmOpen, setRestoreConfirmOpen] = useState(false);
  const [checkpointName, setCheckpointName] = useState('');
  const [checkpoints, setCheckpoints] = useState<SessionCheckpointDto[]>([]);
  const [selectedCheckpoint, setSelectedCheckpoint] = useState<SessionCheckpointDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = useCallback(async () => {
    if (!checkpointName.trim()) return;
    setLoading(true);
    setError(null);
    try {
      await api.sessionTracking.createCheckpoint(sessionId, { name: checkpointName });
      setCheckpointName('');
      setSaveDialogOpen(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save checkpoint');
    } finally {
      setLoading(false);
    }
  }, [sessionId, checkpointName]);

  const handleLoadList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.sessionTracking.listCheckpoints(sessionId);
      setCheckpoints(result.checkpoints);
      setListDialogOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load checkpoints');
    } finally {
      setLoading(false);
    }
  }, [sessionId]);

  const handleRestore = useCallback(async () => {
    if (!selectedCheckpoint) return;
    setLoading(true);
    setError(null);
    try {
      await api.sessionTracking.restoreCheckpoint(sessionId, selectedCheckpoint.id);
      setRestoreConfirmOpen(false);
      setListDialogOpen(false);
      setSelectedCheckpoint(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to restore checkpoint');
    } finally {
      setLoading(false);
    }
  }, [sessionId, selectedCheckpoint]);

  const confirmRestore = useCallback((checkpoint: SessionCheckpointDto) => {
    setSelectedCheckpoint(checkpoint);
    setRestoreConfirmOpen(true);
  }, []);

  return (
    <div className="flex gap-2">
      {error && (
        <p className="text-sm text-red-500" role="alert">
          {error}
        </p>
      )}

      <Dialog open={saveDialogOpen} onOpenChange={setSaveDialogOpen}>
        <DialogTrigger asChild>
          <Button variant="outline" size="sm">
            Save Checkpoint
          </Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Save Checkpoint</DialogTitle>
            <DialogDescription>Create a snapshot of the current session state.</DialogDescription>
          </DialogHeader>
          <Input
            placeholder="Checkpoint name"
            value={checkpointName}
            onChange={e => setCheckpointName(e.target.value)}
            maxLength={200}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setSaveDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleSave} disabled={loading || !checkpointName.trim()}>
              {loading ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Button variant="outline" size="sm" onClick={handleLoadList} disabled={loading}>
        {loading ? 'Loading...' : 'Load Checkpoint'}
      </Button>

      <Dialog open={listDialogOpen} onOpenChange={setListDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Session Checkpoints</DialogTitle>
            <DialogDescription>Select a checkpoint to restore.</DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-64">
            {checkpoints.length === 0 ? (
              <p className="text-sm text-muted-foreground py-4 text-center">
                No checkpoints saved yet.
              </p>
            ) : (
              <div className="space-y-2">
                {checkpoints.map(cp => (
                  <div
                    key={cp.id}
                    className="flex items-center justify-between rounded-md border p-3"
                  >
                    <div>
                      <p className="text-sm font-medium">{cp.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {new Date(cp.createdAt).toLocaleString()} | {cp.diaryEventCount} events
                      </p>
                    </div>
                    <Button variant="outline" size="sm" onClick={() => confirmRestore(cp)}>
                      Restore
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </ScrollArea>
        </DialogContent>
      </Dialog>

      <AlertDialog open={restoreConfirmOpen} onOpenChange={setRestoreConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Restore Checkpoint?</AlertDialogTitle>
            <AlertDialogDescription>
              This will restore the session state to &quot;{selectedCheckpoint?.name}&quot;. Current
              widget states will be overwritten.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleRestore} disabled={loading}>
              {loading ? 'Restoring...' : 'Restore'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
