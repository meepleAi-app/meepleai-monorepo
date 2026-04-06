'use client';

/**
 * Step 4: Creation Progress
 *
 * - Calls executeImport() on mount (ImportGameFromPdfCommand saga)
 * - Shows live status labels: Creazione gioco... → Associazione documento... → Avvio indicizzazione...
 * - On saga success → auto-advances to Step 5
 * - On saga failure → shows error with optional recovery link to created game
 */

import { useEffect, useRef, useState } from 'react';
import type { JSX } from 'react';

import { AlertCircle, CheckCircle, ExternalLink, Loader2 } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

type SagaPhase = 'creating' | 'linking' | 'indexing' | 'done' | 'error';

const PHASE_LABELS: Record<SagaPhase, string> = {
  creating: 'Creazione gioco...',
  linking: 'Associazione documento...',
  indexing: 'Avvio indicizzazione...',
  done: 'Gioco creato con successo!',
  error: 'Creazione fallita',
};

export function Step4CreationProgress(): JSX.Element {
  const {
    importResult,
    isProcessing: _isProcessing,
    error,
    executeImport,
    goNext,
  } = useGameImportWizardStore();

  const [phase, setPhase] = useState<SagaPhase>('creating');
  const hasStarted = useRef(false);
  const goNextTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Start the import saga once on mount
  useEffect(() => {
    if (hasStarted.current) return;
    hasStarted.current = true;

    // Simulate progress phases while the saga runs
    const t1 = setTimeout(() => setPhase('linking'), 1200);
    const t2 = setTimeout(() => setPhase('indexing'), 2400);

    executeImport()
      .then(() => {
        setPhase('done');
        // Short delay then auto-advance to Step 5
        goNextTimerRef.current = setTimeout(() => goNext(), 1200);
      })
      .catch(() => {
        setPhase('error');
      })
      .finally(() => {
        clearTimeout(t1);
        clearTimeout(t2);
      });

    return () => {
      clearTimeout(t1);
      clearTimeout(t2);
      if (goNextTimerRef.current) clearTimeout(goNextTimerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-col items-center gap-6 py-8">
      {/* Phase indicator */}
      <div className="flex flex-col items-center gap-3">
        {phase === 'done' ? (
          <CheckCircle className="h-12 w-12 text-green-500" />
        ) : phase === 'error' ? (
          <AlertCircle className="h-12 w-12 text-destructive" />
        ) : (
          <Loader2 className="h-12 w-12 animate-spin text-primary" />
        )}

        <p className="text-lg font-semibold">{PHASE_LABELS[phase]}</p>

        {phase === 'done' && (
          <p className="text-sm text-muted-foreground">
            Indicizzazione RAG avviata in background. Passaggio al pannello di test...
          </p>
        )}

        {phase !== 'done' && phase !== 'error' && (
          <p className="text-sm text-muted-foreground">
            Operazione in corso, non chiudere la pagina.
          </p>
        )}
      </div>

      {/* Progress steps */}
      <div className="w-full max-w-sm space-y-2">
        {(
          [
            { key: 'creating', label: 'Creazione gioco nel catalogo' },
            { key: 'linking', label: 'Associazione PDF al gioco' },
            { key: 'indexing', label: 'Accodamento indicizzazione RAG' },
          ] as const
        ).map(({ key, label }) => {
          const phases: SagaPhase[] = ['creating', 'linking', 'indexing', 'done'];
          const currentIndex = phases.indexOf(phase); // -1 for 'error'
          const stepIndex = phases.indexOf(key);
          const isDone = phase === 'done' || (currentIndex !== -1 && currentIndex > stepIndex);
          const isCurrent = phase === key;
          const isPending = currentIndex >= 0 && currentIndex < stepIndex;

          return (
            <div key={key} className="flex items-center gap-3 text-sm">
              <div
                className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-medium ${
                  isDone
                    ? 'bg-green-500 text-white'
                    : isCurrent
                      ? 'bg-primary text-primary-foreground'
                      : isPending
                        ? 'bg-muted text-muted-foreground'
                        : 'bg-destructive/20 text-destructive'
                }`}
              >
                {isDone ? '✓' : stepIndex + 1}
              </div>
              <span
                className={
                  isCurrent
                    ? 'font-medium'
                    : isDone
                      ? 'text-muted-foreground line-through'
                      : 'text-muted-foreground'
                }
              >
                {label}
              </span>
              {isCurrent && !isDone && currentIndex !== -1 && (
                <Loader2 className="ml-auto h-3.5 w-3.5 animate-spin text-primary" />
              )}
            </div>
          );
        })}
      </div>

      {/* Warning (indexing enqueue failed but game was created) */}
      {importResult?.warning && (
        <Alert className="w-full max-w-sm">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Avviso</AlertTitle>
          <AlertDescription className="text-sm">{importResult.warning}</AlertDescription>
        </Alert>
      )}

      {/* Error state */}
      {phase === 'error' && error && (
        <div className="w-full max-w-sm space-y-3">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Errore durante la creazione</AlertTitle>
            <AlertDescription className="text-sm">{error}</AlertDescription>
          </Alert>

          {/* Recovery: if game was partially created, link to it */}
          {importResult?.gameId && (
            <div className="rounded-md border bg-muted/30 p-3 text-sm">
              <p className="font-medium">Il gioco è stato creato parzialmente.</p>
              <p className="mt-1 text-muted-foreground">
                Puoi visitare la scheda e associare manualmente il documento.
              </p>
              <a
                href={`/admin/shared-games/${importResult.gameId}`}
                className="mt-2 inline-flex items-center gap-1 text-primary underline underline-offset-2 hover:text-primary/80"
              >
                <ExternalLink className="h-3.5 w-3.5" />
                Apri scheda gioco
              </a>
            </div>
          )}

          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              hasStarted.current = false;
              setPhase('creating');
              // Re-trigger saga
              const t1 = setTimeout(() => setPhase('linking'), 1200);
              const t2 = setTimeout(() => setPhase('indexing'), 2400);
              hasStarted.current = true;
              executeImport()
                .then(() => {
                  setPhase('done');
                  goNextTimerRef.current = setTimeout(() => goNext(), 1200);
                })
                .catch(() => setPhase('error'))
                .finally(() => {
                  clearTimeout(t1);
                  clearTimeout(t2);
                });
            }}
          >
            Riprova
          </Button>
        </div>
      )}
    </div>
  );
}
