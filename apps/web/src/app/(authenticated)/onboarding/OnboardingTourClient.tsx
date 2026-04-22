'use client';

import type { JSX } from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';

import { useRouter } from 'next/navigation';

import {
  AGENTS,
  AgentToggleStep,
  CompleteStep,
  FirstSessionStep,
  GameSelectStep,
  MIN_SELECTED,
  STEP_ENTITIES,
  STEP_LABELS,
  WelcomeStep,
  type AgentStates,
  type FirstSessionChoice,
} from '@/components/onboarding/tour';
import { Btn } from '@/components/ui/v2/btn';
import { StepProgress, type StepEntityKey } from '@/components/ui/v2/step-progress';
import { createApiClient } from '@/lib/api';

const STORAGE_KEY = 'mai-onboarding-step';
const VALID_STEPS = [0, 1, 2, 3, 4] as const;
type TourStep = (typeof VALID_STEPS)[number];

function readStoredStep(): TourStep | null {
  if (typeof window === 'undefined') return null;
  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (raw === null) return null;
  const parsed = Number.parseInt(raw, 10);
  if (!Number.isFinite(parsed)) return null;
  return (VALID_STEPS as readonly number[]).includes(parsed) ? (parsed as TourStep) : null;
}

function defaultAgentStates(): AgentStates {
  return Object.fromEntries(AGENTS.map(a => [a.id, a.defaultOn]));
}

export interface OnboardingTourClientProps {
  readonly userName: string | null;
  /** Override starting step (used in tests). */
  readonly initialStep?: TourStep;
}

export function OnboardingTourClient({
  userName,
  initialStep,
}: OnboardingTourClientProps): JSX.Element {
  const router = useRouter();
  const api = useMemo(() => createApiClient(), []);

  const [step, setStep] = useState<TourStep>(() => {
    if (initialStep !== undefined) return initialStep;
    return readStoredStep() ?? 0;
  });
  const [selectedGames, setSelectedGames] = useState<string[]>([]);
  const [agentStates, setAgentStates] = useState<AgentStates>(defaultAgentStates);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, String(step));
    } catch {
      // Ignore quota errors — resume becomes no-op.
    }
  }, [step]);

  const go = useCallback((next: TourStep) => {
    setStep(next);
  }, []);

  const complete = useCallback(
    async (redirectTo: string) => {
      setIsSubmitting(true);
      setError(null);
      try {
        await api.auth.completeOnboarding(false);
        try {
          window.localStorage.removeItem(STORAGE_KEY);
        } catch {
          // ignore
        }
        router.push(redirectTo);
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Si è verificato un errore. Riprova.');
      } finally {
        setIsSubmitting(false);
      }
    },
    [api, router]
  );

  const toggleGame = useCallback((id: string) => {
    setSelectedGames(prev => (prev.includes(id) ? prev.filter(g => g !== id) : [...prev, id]));
  }, []);

  const toggleAgent = useCallback((id: string) => {
    setAgentStates(prev => ({ ...prev, [id]: !prev[id] }));
  }, []);

  const handleChoose = useCallback(
    (choice: FirstSessionChoice) => {
      void complete(choice.href);
    },
    [complete]
  );

  const handleHome = useCallback(() => {
    void complete('/library');
  }, [complete]);

  const stepEntity: StepEntityKey =
    step >= 1 && step <= 3 ? (STEP_ENTITIES[step - 1] as StepEntityKey) : 'game';

  const progressSteps = useMemo(() => STEP_LABELS.map(label => ({ label })), []);

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-md flex-col">
      {step >= 1 && step <= 3 && (
        <div className="flex items-center gap-3 px-4 pt-6">
          <StepProgress
            steps={progressSteps}
            currentIndex={step - 1}
            entity={stepEntity}
            ariaLabel="Avanzamento onboarding"
            className="flex-1"
          />
          {step !== 3 && (
            <Btn variant="ghost" size="sm" onClick={() => go(4)}>
              Salta
            </Btn>
          )}
        </div>
      )}
      <main className="flex-1">
        {step === 0 && (
          <WelcomeStep userName={userName} onStart={() => go(1)} onSkip={() => go(4)} />
        )}
        {step === 1 && <GameSelectStep selected={selectedGames} onToggle={toggleGame} />}
        {step === 2 && <AgentToggleStep agentStates={agentStates} onToggle={toggleAgent} />}
        {step === 3 && <FirstSessionStep onChoose={handleChoose} />}
        {step === 4 && (
          <CompleteStep isSubmitting={isSubmitting} error={error} onHome={handleHome} />
        )}
      </main>
      {(step === 1 || step === 2 || step === 3) && (
        <footer className="sticky bottom-0 flex items-center justify-between gap-2 border-t border-border bg-background px-4 py-3">
          {step === 1 ? (
            <span />
          ) : (
            <Btn variant="ghost" onClick={() => go((step - 1) as TourStep)}>
              ← Indietro
            </Btn>
          )}
          {step === 1 && (
            <Btn
              variant="primary"
              entity="game"
              disabled={selectedGames.length < MIN_SELECTED}
              onClick={() => go(2)}
            >
              Avanti → ({selectedGames.length}/{MIN_SELECTED})
            </Btn>
          )}
          {step === 2 && (
            <Btn variant="primary" entity="agent" onClick={() => go(3)}>
              Avanti →
            </Btn>
          )}
          {step === 3 && (
            <Btn variant="ghost" onClick={() => go(4)}>
              Salta
            </Btn>
          )}
        </footer>
      )}
    </div>
  );
}
