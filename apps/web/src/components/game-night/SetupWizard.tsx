/**
 * SetupWizard — 3-step guided game preparation
 *
 * Issue #5583: Setup Wizard — guided game preparation (FRONTEND)
 *
 * Steps:
 *   1. Players — add players (name, color), assign roles (Host/Player), configure turn order
 *   2. Physical Setup — checklist from analysis, initial resources, expansion toggles
 *   3. Rules Summary — summary, mechanics badges, victory conditions, FAQs, "Start Game"
 *
 * APIs:
 *   - GET /api/v1/shared-games/{id}/analysis (RulebookAnalysis)
 *   - GET /api/v1/library/entity-links?entityType=Game&entityId={id}&linkType=ExpansionOf
 *
 * Presented as a Sheet (side drawer) for lightweight overlay UX.
 */

'use client';

import { useState, useCallback } from 'react';

import { ArrowLeft, ArrowRight, Loader2, Play } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { WizardSteps } from '@/components/wizard';
import { useGameAnalysis, useGameExpansions } from '@/hooks/queries';

import { PhysicalSetup } from './PhysicalSetup';
import { PlayerSetup, type SetupPlayer } from './PlayerSetup';
import { RulesSummary } from './RulesSummary';

// ========== Types ==========

export interface SetupWizardResult {
  players: SetupPlayer[];
  activeExpansionIds: string[];
}

interface SetupWizardProps {
  /** Shared game UUID — used to fetch analysis and expansions */
  gameId: string;
  /** Game title for display */
  gameTitle: string;
  /** Whether the wizard Sheet is open */
  open: boolean;
  /** Callback when the Sheet is closed */
  onOpenChange: (open: boolean) => void;
  /** Callback when the user clicks "Start Game" */
  onStartGame: (result: SetupWizardResult) => void;
  /** Whether the start game action is loading */
  isStarting?: boolean;
}

// ========== Step Config ==========

const WIZARD_STEPS = [
  { id: 'players', label: 'Giocatori', description: 'Nomi, colori, ruoli' },
  { id: 'setup', label: 'Preparazione', description: 'Checklist e risorse' },
  { id: 'rules', label: 'Regole', description: 'Riepilogo e avvio' },
];

// ========== Component ==========

export function SetupWizard({
  gameId,
  gameTitle,
  open,
  onOpenChange,
  onStartGame,
  isStarting = false,
}: SetupWizardProps) {
  const [currentStep, setCurrentStep] = useState<string>('players');

  // Wizard state
  const [players, setPlayers] = useState<SetupPlayer[]>([]);
  const [activeExpansionIds, setActiveExpansionIds] = useState<string[]>([]);

  // Fetch analysis and expansions
  const { data: analysis, isLoading: analysisLoading } = useGameAnalysis(gameId, open);

  const { data: expansions, isLoading: expansionsLoading } = useGameExpansions(gameId, open);

  const isDataLoading = analysisLoading || expansionsLoading;

  // ========== Navigation ==========

  const currentIndex = WIZARD_STEPS.findIndex(s => s.id === currentStep);

  const canProceed = useCallback((): boolean => {
    switch (currentStep) {
      case 'players':
        return players.length >= 1;
      case 'setup':
        return true; // always can proceed
      case 'rules':
        return true;
      default:
        return false;
    }
  }, [currentStep, players.length]);

  const goNext = useCallback(() => {
    if (currentIndex < WIZARD_STEPS.length - 1) {
      setCurrentStep(WIZARD_STEPS[currentIndex + 1].id);
    }
  }, [currentIndex]);

  const goBack = useCallback(() => {
    if (currentIndex > 0) {
      setCurrentStep(WIZARD_STEPS[currentIndex - 1].id);
    }
  }, [currentIndex]);

  const handleStepClick = useCallback(
    (stepId: string) => {
      const targetIndex = WIZARD_STEPS.findIndex(s => s.id === stepId);
      // Only allow clicking completed or current steps
      if (targetIndex <= currentIndex) {
        setCurrentStep(stepId);
      }
    },
    [currentIndex]
  );

  const handleStartGame = useCallback(() => {
    onStartGame({ players, activeExpansionIds });
  }, [players, activeExpansionIds, onStartGame]);

  // ========== Render ==========

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        className="w-full sm:max-w-lg overflow-y-auto"
        aria-describedby="setup-wizard-description"
      >
        <SheetHeader className="mb-4">
          <SheetTitle className="font-quicksand">Prepara la partita</SheetTitle>
          <p id="setup-wizard-description" className="text-sm text-muted-foreground">
            {gameTitle}
          </p>
        </SheetHeader>

        {/* Stepper */}
        <WizardSteps
          steps={WIZARD_STEPS}
          currentStep={currentStep}
          onStepClick={handleStepClick}
          allowSkip={false}
        />

        {/* Loading state */}
        {isDataLoading && currentStep !== 'players' && (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            <span className="ml-2 text-sm text-muted-foreground">
              Caricamento dati del gioco...
            </span>
          </div>
        )}

        {/* Step content */}
        {(!isDataLoading || currentStep === 'players') && (
          <div className="py-2">
            {currentStep === 'players' && (
              <PlayerSetup players={players} onPlayersChange={setPlayers} />
            )}

            {currentStep === 'setup' && (
              <PhysicalSetup
                gamePhases={analysis?.gamePhases ?? []}
                resources={analysis?.resources ?? []}
                expansions={expansions ?? []}
                activeExpansionIds={activeExpansionIds}
                onActiveExpansionsChange={setActiveExpansionIds}
                hasNoAnalysis={!analysis}
              />
            )}

            {currentStep === 'rules' && (
              <RulesSummary analysis={analysis ?? null} gameTitle={gameTitle} />
            )}
          </div>
        )}

        {/* Navigation footer */}
        <div className="flex items-center justify-between pt-4 mt-4 border-t border-border">
          <Button
            variant="ghost"
            onClick={currentIndex === 0 ? () => onOpenChange(false) : goBack}
            disabled={isStarting}
          >
            <ArrowLeft className="h-4 w-4 mr-1" />
            {currentIndex === 0 ? 'Annulla' : 'Indietro'}
          </Button>

          {currentStep !== 'rules' ? (
            <Button onClick={goNext} disabled={!canProceed()}>
              Avanti
              <ArrowRight className="h-4 w-4 ml-1" />
            </Button>
          ) : (
            <Button
              onClick={handleStartGame}
              disabled={!canProceed() || isStarting || players.length < 1}
              className="bg-indigo-600 hover:bg-indigo-700"
            >
              {isStarting ? (
                <>
                  <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                  Avvio...
                </>
              ) : (
                <>
                  <Play className="h-4 w-4 mr-1" />
                  Avvia Partita
                </>
              )}
            </Button>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
