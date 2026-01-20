/**
 * Player Mode Tour - Issue #2475
 *
 * Interactive guided tour for first-time Player Mode users.
 *
 * Features:
 * - Step-by-step walkthrough of Player Mode features
 * - localStorage tracking for "tour completed" state
 * - Spotlight on key UI elements
 * - Skip/restart tour capability
 */

'use client';

import { useCallback, useState, useEffect } from 'react';

import Joyride, { Step, CallBackProps, STATUS, EVENTS } from 'react-joyride';

// ========== Tour Steps ==========

const TOUR_STEPS: Step[] = [
  {
    target: 'body',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Benvenuto in Player Mode! 🎮</h3>
        <p className="text-sm text-muted-foreground">
          Ti guiderò attraverso le funzionalità principali dell&apos;assistente AI per i tuoi
          giochi da tavolo.
        </p>
      </div>
    ),
    placement: 'center',
    disableBeacon: true,
  },
  {
    target: '[data-tour="suggest-button"]',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Richiedi Suggerimenti AI ✨</h3>
        <p className="text-sm text-muted-foreground mb-2">
          Clicca questo pulsante per richiedere un suggerimento sulla prossima mossa da effettuare.
        </p>
        <p className="text-xs text-muted-foreground">
          L&apos;AI analizzerà lo stato del gioco e le regole strategiche per consigliarti la
          mossa ottimale.
        </p>
      </div>
    ),
    placement: 'bottom',
  },
  {
    target: '[data-tour="confidence-meter"]',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Meter di Confidenza 📊</h3>
        <p className="text-sm text-muted-foreground mb-2">
          Questo indicatore mostra quanto l&apos;AI è sicura del suggerimento.
        </p>
        <ul className="text-xs space-y-1 text-muted-foreground">
          <li>• <span className="font-medium">Alto (&gt;80%)</span>: Mossa fortemente consigliata</li>
          <li>• <span className="font-medium">Medio (50-80%)</span>: Valida ma con rischi</li>
          <li>• <span className="font-medium">Basso (&lt;50%)</span>: Valuta con cautela</li>
        </ul>
      </div>
    ),
    placement: 'top',
  },
  {
    target: '[data-tour="primary-suggestion"]',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Suggerimento Principale 💡</h3>
        <p className="text-sm text-muted-foreground mb-2">
          Questa è la mossa con la confidenza più alta. Include:
        </p>
        <ul className="text-xs space-y-1 text-muted-foreground">
          <li>• Azione specifica da effettuare</li>
          <li>• Rationale (perché è consigliata)</li>
          <li>• Risultato atteso dopo la mossa</li>
        </ul>
      </div>
    ),
    placement: 'top',
  },
  {
    target: '[data-tour="alternative-moves"]',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Mosse Alternative 🔄</h3>
        <p className="text-sm text-muted-foreground">
          Opzioni alternative con confidenza inferiore. Utili se la mossa primaria non è
          applicabile o vuoi esplorare strategie diverse.
        </p>
      </div>
    ),
    placement: 'top',
  },
  {
    target: '[data-tour="actions"]',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Applica o Ignora ✓ / ✗</h3>
        <p className="text-sm text-muted-foreground mb-2">
          Dopo aver valutato il suggerimento:
        </p>
        <ul className="text-xs space-y-1 text-muted-foreground">
          <li>• <span className="font-medium">Applica</span>: Conferma l&apos;utilizzo del suggerimento</li>
          <li>• <span className="font-medium">Ignora</span>: Nasconde il suggerimento corrente</li>
        </ul>
        <p className="text-xs text-muted-foreground mt-2">
          Puoi richiedere nuovi suggerimenti in qualsiasi momento!
        </p>
      </div>
    ),
    placement: 'top',
  },
  {
    target: '[data-tour="help-button"]',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Serve Aiuto? 🆘</h3>
        <p className="text-sm text-muted-foreground">
          Clicca qui per accedere alla guida completa con FAQ, spiegazioni dettagliate sulla
          confidenza e come interpretare i suggerimenti.
        </p>
      </div>
    ),
    placement: 'bottom',
  },
  {
    target: 'body',
    content: (
      <div>
        <h3 className="text-lg font-semibold mb-2">Sei Pronto! 🚀</h3>
        <p className="text-sm text-muted-foreground mb-2">
          Hai completato il tour di Player Mode. Ora puoi:
        </p>
        <ul className="text-xs space-y-1 text-muted-foreground">
          <li>• Richiedere suggerimenti AI per le tue partite</li>
          <li>• Interpretare la confidenza dei suggerimenti</li>
          <li>• Valutare mosse alternative</li>
          <li>• Accedere alla guida completa quando serve</li>
        </ul>
        <p className="text-xs text-muted-foreground mt-2">
          Buon divertimento! 🎲
        </p>
      </div>
    ),
    placement: 'center',
  },
];

// ========== LocalStorage Keys ==========

const TOUR_COMPLETED_KEY = 'playerModeTourCompleted';

// ========== Component Props ==========

export interface PlayerModeTourProps {
  /** Run tour immediately on mount */
  autoStart?: boolean;
  /** Callback when tour is completed */
  onTourComplete?: () => void;
  /** Force run tour even if already completed */
  forceRun?: boolean;
}

// ========== Main Component ==========

export function PlayerModeTour({ autoStart = false, onTourComplete, forceRun = false }: PlayerModeTourProps) {
  const [runTour, setRunTour] = useState(false);

  useEffect(() => {
    // Check if tour was already completed
    const tourCompleted = localStorage.getItem(TOUR_COMPLETED_KEY);

    if (forceRun || (!tourCompleted && autoStart)) {
      setRunTour(true);
    }
  }, [autoStart, forceRun]);

  const handleJoyrideCallback = useCallback(
    (data: CallBackProps) => {
      const { status, type } = data;

      // Tour finished or skipped
      if (status === STATUS.FINISHED || status === STATUS.SKIPPED) {
        setRunTour(false);

        // Mark as completed in localStorage
        localStorage.setItem(TOUR_COMPLETED_KEY, 'true');

        // Trigger callback
        if (onTourComplete) {
          onTourComplete();
        }
      }

      // Log events for debugging
      if (type === EVENTS.TOUR_END) {
        // eslint-disable-next-line no-console
        console.log('Player Mode tour completed');
      }
    },
    [onTourComplete]
  );

  return (
    <Joyride
      steps={TOUR_STEPS}
      run={runTour}
      continuous
      showProgress
      showSkipButton
      callback={handleJoyrideCallback}
      styles={{
        options: {
          primaryColor: 'hsl(var(--primary))',
          zIndex: 10000,
        },
      }}
      locale={{
        back: 'Indietro',
        close: 'Chiudi',
        last: 'Fine',
        next: 'Avanti',
        skip: 'Salta',
      }}
    />
  );
}

/**
 * Hook to programmatically start the Player Mode tour
 */
export function usePlayerModeTour() {
  const [runTour, setRunTour] = useState(false);

  const startTour = useCallback(() => {
    setRunTour(true);
  }, []);

  const resetTour = useCallback(() => {
    localStorage.removeItem(TOUR_COMPLETED_KEY);
    setRunTour(true);
  }, []);

  return { runTour, startTour, resetTour, setRunTour };
}
