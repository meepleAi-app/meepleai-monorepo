'use client';

import { Gamepad2 } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { ActivationStep } from './ActivationStep';

export type PdfStatus = 'none' | 'uploading' | 'processing' | 'ready' | 'failed';
export type AgentStatus = 'none' | 'creating' | 'ready';

interface ActivationChecklistProps {
  gameAdded: boolean;
  pdfStatus: PdfStatus;
  agentStatus: AgentStatus;
  onUploadPdf: () => void;
  onCreateAgent: () => void;
  onStartGame: () => void;
  children?: React.ReactNode;
}

export function ActivationChecklist({
  gameAdded,
  pdfStatus,
  agentStatus,
  onUploadPdf,
  onCreateAgent,
  onStartGame,
  children,
}: ActivationChecklistProps) {
  const pdfReady = pdfStatus === 'ready';
  const agentReady = agentStatus === 'ready';
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
        title="Carica il regolamento (PDF)"
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

      <ActivationStep
        stepNumber={3}
        title="Agente AI pronto"
        completed={agentReady}
        collapsed={agentReady}
        disabled={!pdfReady}
        testId="step-agent"
      >
        {pdfReady && agentStatus === 'none' && (
          <Button variant="outline" size="sm" onClick={onCreateAgent}>
            Crea agente
          </Button>
        )}
        {agentStatus === 'creating' && (
          <p className="text-sm text-muted-foreground">Creazione in corso...</p>
        )}
      </ActivationStep>

      <Button className="w-full mt-4" size="lg" disabled={!canStartGame} onClick={onStartGame}>
        <Gamepad2 className="mr-2 h-5 w-5" />
        Inizia Partita
      </Button>
      {!canStartGame && (
        <p className="text-xs text-center text-muted-foreground">
          Completa almeno i primi 2 step per iniziare
        </p>
      )}
    </div>
  );
}
