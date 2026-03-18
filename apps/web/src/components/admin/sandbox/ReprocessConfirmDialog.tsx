'use client';

import { AlertTriangle } from 'lucide-react';

import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from '@/components/ui/overlays/alert-dialog-primitives';

interface ReprocessConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  chunksToDelete: number;
  vectorsToDelete: number;
  estimatedTimeSeconds: number;
}

function formatTime(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return secs > 0 ? `${mins}m ${secs}s` : `${mins}m`;
}

export function ReprocessConfirmDialog({
  open,
  onOpenChange,
  onConfirm,
  chunksToDelete,
  vectorsToDelete,
  estimatedTimeSeconds,
}: ReprocessConfirmDialogProps) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <span className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-amber-100">
              <AlertTriangle className="h-4 w-4 text-amber-600" />
            </span>
            Conferma rielaborazione
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3">
              <p>
                La modifica dei parametri di chunking richiede la rielaborazione dei documenti.
                Questa operazione comporta:
              </p>
              <div className="rounded-md bg-amber-50 p-3 space-y-1 text-sm">
                <div className="flex justify-between">
                  <span className="text-amber-800">Chunk da eliminare</span>
                  <span className="font-mono font-medium text-amber-900" data-testid="chunks-count">
                    {chunksToDelete.toLocaleString()}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-amber-800">Vettori da eliminare</span>
                  <span
                    className="font-mono font-medium text-amber-900"
                    data-testid="vectors-count"
                  >
                    {vectorsToDelete.toLocaleString()}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-amber-800">Tempo stimato</span>
                  <span
                    className="font-mono font-medium text-amber-900"
                    data-testid="estimated-time"
                  >
                    {formatTime(estimatedTimeSeconds)}
                  </span>
                </div>
              </div>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Annulla</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Rielabora e Applica</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
