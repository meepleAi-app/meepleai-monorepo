'use client';

/**
 * GameNightWizard — 3-step quick start for improvised game nights.
 *
 * Steps:
 *   1. Find game (catalog → BGG)
 *   2. Upload rules PDF (optional, with copyright disclaimer)
 *   3. Create session with players
 *
 * Issue #123 — Game Night Quick Start Wizard
 */

import { useCallback, useState } from 'react';

import { WizardSteps } from '@/components/wizard/WizardSteps';

import { CreateSessionStep } from './steps/CreateSessionStep';
import { SearchGameStep } from './steps/SearchGameStep';
import { UploadRulesStep } from './steps/UploadRulesStep';

// ============================================================================
// Types
// ============================================================================

type WizardStep = 'search' | 'upload' | 'session';

interface GameNightWizardProps {
  onComplete: (sessionId: string) => void;
}

interface WizardState {
  gameId?: string;
  gameTitle?: string;
  privateGameId?: string;
  pdfId?: string;
}

const WIZARD_STEPS = [
  { id: 'search', label: 'Trova il gioco' },
  { id: 'upload', label: 'Regolamento' },
  { id: 'session', label: 'Giocatori' },
];

// ============================================================================
// Component
// ============================================================================

export function GameNightWizard({ onComplete }: GameNightWizardProps) {
  const [step, setStep] = useState<WizardStep>('search');
  const [state, setState] = useState<WizardState>({});

  const handleGameFound = useCallback(
    (data: { gameId?: string; privateGameId?: string; gameTitle: string }) => {
      setState(prev => ({ ...prev, ...data }));
      setStep('upload');
    },
    []
  );

  const handleUploadComplete = useCallback((pdfId?: string) => {
    setState(prev => ({ ...prev, pdfId }));
    setStep('session');
  }, []);

  const handleSessionCreated = useCallback(
    (sessionId: string) => {
      onComplete(sessionId);
    },
    [onComplete]
  );

  return (
    <div className="space-y-6" data-testid="game-night-wizard">
      <WizardSteps steps={WIZARD_STEPS} currentStep={step} />

      {step === 'search' && <SearchGameStep onGameFound={handleGameFound} />}

      {step === 'upload' && (
        <UploadRulesStep
          gameId={state.gameId}
          privateGameId={state.privateGameId}
          gameTitle={state.gameTitle ?? ''}
          onComplete={handleUploadComplete}
          onSkip={() => handleUploadComplete()}
        />
      )}

      {step === 'session' && (
        <CreateSessionStep
          gameId={state.gameId}
          gameTitle={state.gameTitle ?? ''}
          onSessionCreated={handleSessionCreated}
        />
      )}
    </div>
  );
}
