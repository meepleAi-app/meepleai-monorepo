'use client';

/**
 * New Session Page
 *
 * Issue #5041 — Sessions Redesign Phase 1
 * Issue #123 — Game Night Quick Start Wizard entry point
 * Phase 5: Game Night — Task 3 (SessionWizardMobile on mobile)
 *
 * Shows "Serata di Gioco" quick start option + existing 4-step wizard.
 * On mobile (<lg), renders the simplified SessionWizardMobile instead.
 */

import { useCallback, useState } from 'react';

import { PartyPopper } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GameNightWizard } from '@/components/game-night/GameNightWizard';
import { SessionCreationWizard } from '@/components/session/SessionCreationWizard';
import { Button } from '@/components/ui/primitives/button';

import { SessionWizardMobile } from './session-wizard-mobile';

export default function NewSessionPage() {
  const [showWizard, setShowWizard] = useState(false);
  const router = useRouter();

  const handleWizardComplete = useCallback(
    (sessionId: string) => {
      router.push(`/sessions/${sessionId}/play`);
    },
    [router]
  );

  return (
    <>
      {/* Mobile: simplified 3-step wizard */}
      <div className="lg:hidden container mx-auto px-4 py-6">
        <SessionWizardMobile />
      </div>

      {/* Desktop: full wizard */}
      <div className="hidden lg:block container mx-auto px-4 py-6 space-y-6">
        {/* Game Night Quick Start */}
        {!showWizard ? (
          <div
            className="rounded-xl border-2 border-amber-500/30 bg-amber-500/5 p-6"
            data-testid="game-night-entry"
          >
            <h2 className="font-quicksand font-bold text-xl mb-2">Serata di Gioco</h2>
            <p className="text-sm text-muted-foreground mb-4">
              Trova un gioco, carica il regolamento e inizia subito — l&apos;agente AI ti assiste.
            </p>
            <Button
              onClick={() => setShowWizard(true)}
              className="bg-amber-600 hover:bg-amber-700 text-white"
              data-testid="start-game-night-button"
            >
              <PartyPopper className="h-4 w-4 mr-2" aria-hidden="true" />
              Inizia Serata di Gioco
            </Button>
          </div>
        ) : (
          <div className="rounded-xl border border-slate-200 dark:border-slate-700 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-quicksand font-bold text-xl">Serata di Gioco</h2>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setShowWizard(false)}
                data-testid="close-wizard-button"
              >
                Chiudi
              </Button>
            </div>
            <GameNightWizard onComplete={handleWizardComplete} />
          </div>
        )}

        {/* Existing Session Creation Wizard */}
        {!showWizard && <SessionCreationWizard />}
      </div>
    </>
  );
}
