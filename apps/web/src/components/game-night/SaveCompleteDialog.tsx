'use client';

/**
 * SaveCompleteDialog — Enhanced save dialog that orchestrates
 * pause + snapshot + agent persist + recap generation.
 *
 * Replaces the basic PauseSessionDialog for the "save complete" flow.
 *
 * Issue #122 — Enhanced Save/Resume
 */

import { useState, useCallback } from 'react';

import { Camera, Check, Loader2, Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { api } from '@/lib/api';
import type { SessionSaveResult } from '@/lib/api/schemas/save-resume.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface SaveCompleteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sessionId: string;
  onSaveComplete: () => void;
  className?: string;
}

type SavePhase = 'confirm' | 'saving' | 'done' | 'error';

// ============================================================================
// Component
// ============================================================================

export function SaveCompleteDialog({
  open,
  onOpenChange,
  sessionId,
  onSaveComplete,
}: SaveCompleteDialogProps) {
  const [phase, setPhase] = useState<SavePhase>('confirm');
  const [result, setResult] = useState<SessionSaveResult | null>(null);
  const [errorMsg, setErrorMsg] = useState('');

  const handleSave = useCallback(async () => {
    setPhase('saving');
    setErrorMsg('');

    try {
      const saveResult = await api.liveSessions.saveComplete(sessionId);
      setResult(saveResult);
      setPhase('done');
    } catch {
      setErrorMsg('Errore durante il salvataggio. Riprova.');
      setPhase('error');
    }
  }, [sessionId]);

  const handleClose = useCallback(() => {
    const wasDone = phase === 'done';
    onOpenChange(false);
    if (wasDone) onSaveComplete();
    // Reset state for next open
    setPhase('confirm');
    setResult(null);
    setErrorMsg('');
  }, [onOpenChange, onSaveComplete, phase]);

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-sm" data-testid="save-complete-dialog">
        <DialogTitle className="flex items-center gap-2">
          <Save className="h-5 w-5 text-amber-600" aria-hidden="true" />
          Salva Stato Completo
        </DialogTitle>

        {/* Confirm phase */}
        {phase === 'confirm' && (
          <div className="space-y-4 pt-2">
            <p className="text-sm text-muted-foreground">
              Vuoi salvare lo stato della partita? Potrai riprendere in un secondo momento.
            </p>
            <div className="space-y-2">
              <p className="text-xs font-medium text-slate-500 dark:text-slate-400">
                Il salvataggio include:
              </p>
              <ul className="text-xs space-y-1 ml-4 list-disc text-muted-foreground">
                <li>Punteggi e stato del turno</li>
                <li>Memoria dell&apos;agente AI</li>
                <li>Snapshot della partita</li>
              </ul>
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" size="sm" onClick={handleClose}>
                Annulla
              </Button>
              <Button
                size="sm"
                onClick={handleSave}
                className="bg-amber-600 hover:bg-amber-700 text-white"
                data-testid="save-complete-confirm"
              >
                <Save className="h-4 w-4 mr-2" aria-hidden="true" />
                Salva Tutto
              </Button>
            </div>
          </div>
        )}

        {/* Saving phase */}
        {phase === 'saving' && (
          <div className="flex flex-col items-center justify-center gap-3 py-8">
            <Loader2 className="h-8 w-8 animate-spin text-amber-500" aria-hidden="true" />
            <p className="text-sm text-muted-foreground">Salvataggio in corso...</p>
          </div>
        )}

        {/* Done phase */}
        {phase === 'done' && result && (
          <div className="space-y-3 pt-2">
            <div
              className={cn(
                'rounded-lg border p-3 text-sm',
                'border-emerald-500/30 bg-emerald-50 dark:bg-emerald-900/20',
                'text-emerald-800 dark:text-emerald-300'
              )}
              data-testid="save-complete-success"
            >
              <div className="flex items-start gap-2">
                <Check className="h-4 w-4 mt-0.5 flex-shrink-0" aria-hidden="true" />
                <div>
                  <p className="font-medium">Partita salvata</p>
                  <p className="text-xs mt-1 opacity-80">{result.recap}</p>
                </div>
              </div>
            </div>
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>Snapshot #{result.snapshotIndex}</span>
              {result.photoCount > 0 && (
                <span className="flex items-center gap-1">
                  <Camera className="h-3 w-3" aria-hidden="true" />
                  {result.photoCount} foto
                </span>
              )}
            </div>
            <div className="flex justify-end pt-1">
              <Button size="sm" onClick={handleClose} data-testid="save-complete-close">
                Chiudi
              </Button>
            </div>
          </div>
        )}

        {/* Error phase */}
        {phase === 'error' && (
          <div className="space-y-3 pt-2">
            <p className="text-sm text-red-600 dark:text-red-400">{errorMsg}</p>
            <div className="flex justify-end gap-2">
              <Button variant="outline" size="sm" onClick={handleClose}>
                Annulla
              </Button>
              <Button size="sm" onClick={handleSave}>
                Riprova
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
