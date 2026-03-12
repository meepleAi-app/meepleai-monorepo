'use client';

/**
 * SaveCompleteDialog — Enhanced save dialog that orchestrates
 * pause + snapshot + agent persist + recap generation.
 *
 * Replaces the basic PauseSessionDialog for the "save complete" flow.
 *
 * Issue #122 — Enhanced Save/Resume
 */

import { useState, useCallback, useRef } from 'react';

import { Bot, Camera, Check, ImagePlus, Loader2, Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { Textarea } from '@/components/ui/primitives/textarea';
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
  const [notes, setNotes] = useState('');
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [aiSummary, setAiSummary] = useState('');
  const [isSummarizing, setIsSummarizing] = useState(false);
  const photoInputRef = useRef<HTMLInputElement>(null);

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

  const handlePhotoSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) setPhotoFile(file);
  }, []);

  const handleGenerateSummary = useCallback(async () => {
    setIsSummarizing(true);
    try {
      // Placeholder — will be wired to real AI summary endpoint later
      setAiSummary('Riepilogo in arrivo nella prossima iterazione.');
    } finally {
      setIsSummarizing(false);
    }
  }, []);

  const handleClose = useCallback(() => {
    const wasDone = phase === 'done';
    onOpenChange(false);
    if (wasDone) onSaveComplete();
    // Reset state for next open
    setPhase('confirm');
    setResult(null);
    setErrorMsg('');
    setNotes('');
    setPhotoFile(null);
    setAiSummary('');
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
            {/* Notes textarea */}
            <Textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              placeholder="Note sulla partita (opzionale)..."
              rows={3}
              className="text-sm"
            />

            {/* Photo upload + AI summary row */}
            <div className="flex items-center gap-2">
              <input
                ref={photoInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={handlePhotoSelect}
                data-testid="photo-file-input"
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => photoInputRef.current?.click()}
              >
                <ImagePlus className="h-4 w-4 mr-1" aria-hidden="true" />
                {photoFile ? photoFile.name : 'Aggiungi foto'}
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleGenerateSummary}
                disabled={isSummarizing}
                aria-label="Genera riepilogo AI"
              >
                {isSummarizing ? (
                  <Loader2 className="h-4 w-4 mr-1 animate-spin" aria-hidden="true" />
                ) : (
                  <Bot className="h-4 w-4 mr-1" aria-hidden="true" />
                )}
                Genera riepilogo AI
              </Button>
            </div>

            {/* AI summary display */}
            {aiSummary && (
              <div
                className={cn(
                  'rounded-lg border p-3 text-xs',
                  'border-blue-500/30 bg-blue-50 dark:bg-blue-900/20',
                  'text-blue-800 dark:text-blue-300'
                )}
                data-testid="ai-summary"
              >
                <p className="font-medium mb-1">Riepilogo AI</p>
                <p>{aiSummary}</p>
              </div>
            )}

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
