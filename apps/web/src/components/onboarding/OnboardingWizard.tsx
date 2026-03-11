'use client';

/**
 * OnboardingWizard Component
 * Issue #132 - Multi-step onboarding for invited users
 *
 * Manages wizard state and step navigation:
 * Step 1: Password setup (required)
 * Step 2: Profile setup (skippable)
 * Step 3: Interests selection (skippable)
 * Step 4: First game search (optional, skippable)
 * Step 5: First agent setup (only if game added in step 4)
 */

import { useCallback, useState } from 'react';

import { useRouter } from 'next/navigation';

import { cn } from '@/lib/utils';

import { FirstAgentStep } from './FirstAgentStep';
import { FirstGameStep } from './FirstGameStep';
import { InterestsStep } from './InterestsStep';
import { PasswordStep } from './PasswordStep';
import { ProfileStep } from './ProfileStep';

export interface OnboardingWizardProps {
  token: string;
  role: string;
}

interface WizardState {
  currentStep: number;
  passwordCompleted: boolean;
  addedGameId: string | null;
  addedGameName: string | null;
}

const STEP_LABELS = ['Password', 'Profile', 'Interests', 'First Game', 'First Agent'];

export function OnboardingWizard({ token, role }: OnboardingWizardProps) {
  const router = useRouter();
  const [state, setState] = useState<WizardState>({
    currentStep: 1,
    passwordCompleted: false,
    addedGameId: null,
    addedGameName: null,
  });

  const hasGame = state.addedGameId !== null;
  const totalSteps = hasGame ? 5 : 4;

  const goToNext = useCallback(() => {
    setState(prev => {
      const next = prev.currentStep + 1;
      const maxSteps = prev.addedGameId ? 5 : 4;
      if (next > maxSteps) {
        return prev;
      }
      return { ...prev, currentStep: next };
    });
  }, []);

  const goToPrev = useCallback(() => {
    setState(prev => ({
      ...prev,
      currentStep: Math.max(1, prev.currentStep - 1),
    }));
  }, []);

  const handlePasswordComplete = useCallback(() => {
    setState(prev => ({
      ...prev,
      passwordCompleted: true,
      currentStep: 2,
    }));
  }, []);

  const handleGameAdded = useCallback((gameId: string, gameName: string) => {
    setState(prev => ({
      ...prev,
      addedGameId: gameId,
      addedGameName: gameName,
    }));
  }, []);

  const handleFinish = useCallback(() => {
    router.push('/chat');
  }, [router]);

  const handleSkipWizard = useCallback(() => {
    if (state.passwordCompleted) {
      router.push('/chat');
    }
  }, [router, state.passwordCompleted]);

  const isLastStep = state.currentStep === totalSteps;

  return (
    <div className="space-y-8">
      {/* Header with skip link */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold text-slate-900">Welcome to MeepleAI</h1>
          <p className="mt-1 text-sm text-slate-600">Set up your account in a few quick steps</p>
        </div>
        {state.passwordCompleted && (
          <button
            type="button"
            onClick={handleSkipWizard}
            className="text-sm text-slate-500 hover:text-slate-700 underline"
            data-testid="skip-wizard"
          >
            Skip wizard
          </button>
        )}
      </div>

      {/* Progress bar */}
      <div className="space-y-3">
        <div className="flex items-center justify-between text-sm text-slate-600">
          <span>
            Step {state.currentStep} of {totalSteps}
          </span>
          <span>{STEP_LABELS[state.currentStep - 1]}</span>
        </div>
        <div className="flex gap-2">
          {Array.from({ length: totalSteps }, (_, i) => (
            <div
              key={i}
              className={cn(
                'h-2 flex-1 rounded-full transition-colors',
                i < state.currentStep
                  ? 'bg-amber-500'
                  : i === state.currentStep - 1
                    ? 'bg-amber-500'
                    : 'bg-slate-200'
              )}
              data-testid={`progress-step-${i + 1}`}
            />
          ))}
        </div>
      </div>

      {/* Step content */}
      <div
        className="rounded-xl border bg-white/70 backdrop-blur-md p-6 shadow-sm"
        data-testid="wizard-step-content"
      >
        {state.currentStep === 1 && (
          <PasswordStep token={token} onComplete={handlePasswordComplete} />
        )}
        {state.currentStep === 2 && <ProfileStep onComplete={goToNext} onSkip={goToNext} />}
        {state.currentStep === 3 && <InterestsStep onComplete={goToNext} onSkip={goToNext} />}
        {state.currentStep === 4 && (
          <FirstGameStep onComplete={goToNext} onSkip={goToNext} onGameAdded={handleGameAdded} />
        )}
        {state.currentStep === 5 && hasGame && (
          <FirstAgentStep
            gameId={state.addedGameId!}
            gameName={state.addedGameName!}
            onComplete={handleFinish}
            onSkip={handleFinish}
          />
        )}
      </div>

      {/* Navigation buttons */}
      <div className="flex items-center justify-between">
        <div>
          {state.currentStep > 1 && (
            <button
              type="button"
              onClick={goToPrev}
              className="text-sm font-medium text-slate-600 hover:text-slate-900"
              data-testid="wizard-back"
            >
              Back
            </button>
          )}
        </div>
        <div className="flex items-center gap-3">
          {state.currentStep > 1 && !isLastStep && (
            <button
              type="button"
              onClick={goToNext}
              className="text-sm text-slate-500 hover:text-slate-700"
              data-testid="wizard-skip"
            >
              Skip
            </button>
          )}
          {isLastStep && state.currentStep > 1 && (
            <button
              type="button"
              onClick={handleFinish}
              className="rounded-lg bg-amber-600 px-6 py-2 text-sm font-medium text-white hover:bg-amber-700 transition-colors"
              data-testid="wizard-finish"
            >
              Finish
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
