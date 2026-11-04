'use client';

import { Gamepad2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { ActivationStep } from './ActivationStep';

export type PdfStatus = 'none' | 'uploading' | 'processing' | 'ready' | 'failed';

/**
 * Issue #4138: the third step, "Agente AI pronto", is gone along with the
 * `agentStatus` / `onCreateAgent` props.
 *
 * Three reasons, and the first alone was enough:
 *   1. It could never complete. The host sent `agentType: 'TutorAgent'`, which
 *      `AgentType.Parse` rejects, so `createUserAgent` always answered 400 and
 *      the failure was swallowed back into `'none'`.
 *   2. It duplicated the backend. `AutoCreateAgentOnPdfReadyHandler` already
 *      links an agent to the private game on `VectorDocumentReadyIntegrationEvent`.
 *   3. With one system-wide agent there is nothing per-game to wait for. What
 *      gates a useful answer is the knowledge base, which step 2 already tracks
 *      — `pdfStatus === 'ready'` means `processingState` is Ready or Indexed.
 *
 * `canStartGame` is unchanged: it never depended on the agent step.
 */
interface ActivationChecklistProps {
  gameAdded: boolean;
  pdfStatus: PdfStatus;
  onUploadPdf: () => void;
  onStartGame: () => void;
  onTryQuestion?: () => void;
  children?: React.ReactNode;
}

export function ActivationChecklist({
  gameAdded,
  pdfStatus,
  onUploadPdf,
  onStartGame,
  onTryQuestion,
  children,
}: ActivationChecklistProps) {
  const pdfReady = pdfStatus === 'ready';
  const canStartGame = gameAdded && pdfReady;

  return (
    <div className="space-y-3">
      <h3 className="text-lg font-semibold">Attiva l&apos;Assistente AI</h3>

      <ActivationStep
        stepNumber={1}
        title="Gioco aggiunto alla libreria"
        completed={gameAdded}
        collapsed={gameAdded}
        testId="step-game-added"
      />

      <ActivationStep
        stepNumber={2}
        title="Knowledge base pronta"
        completed={pdfReady}
        collapsed={pdfReady}
        testId="step-pdf"
      >
        {pdfStatus === 'none' && (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">
              Carica il PDF del regolamento per attivare l&apos;assistente AI.
            </p>
            <Button variant="outline" size="sm" onClick={onUploadPdf}>
              Carica regolamento
            </Button>
          </div>
        )}
        {(pdfStatus === 'uploading' || pdfStatus === 'processing') && children}
        {pdfStatus === 'failed' && (
          <div className="space-y-2">
            <p className="text-sm text-destructive">Elaborazione fallita.</p>
            <Button variant="outline" size="sm" onClick={onUploadPdf}>
              Riprova
            </Button>
          </div>
        )}
      </ActivationStep>

      {/* Outside the steps on purpose: `ActivationStep` hides its children once
          `collapsed`, and step 2 collapses the moment it completes — so a CTA
          nested in it would be invisible exactly when it becomes relevant. */}
      {pdfReady && onTryQuestion && (
        <button
          type="button"
          className="text-sm text-primary hover:underline"
          onClick={onTryQuestion}
        >
          Prova una domanda &rarr;
        </button>
      )}

      <Button className="w-full mt-4" size="lg" disabled={!canStartGame} onClick={onStartGame}>
        <Gamepad2 className="mr-2 h-5 w-5" />
        Inizia Partita
      </Button>
      {!canStartGame && (
        <p className="text-xs text-center text-muted-foreground">Completa i 2 step per iniziare</p>
      )}
    </div>
  );
}
