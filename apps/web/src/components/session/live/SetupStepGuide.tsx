/**
 * SetupStepGuide
 *
 * Setup Wizard — Task 8
 *
 * Step-by-step interactive guide. Shows current step with "Done, next step" button.
 */

'use client';

import { CheckCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface SetupStep {
  order: number;
  instruction: string;
  completed: boolean;
}

interface SetupStepGuideProps {
  steps: SetupStep[];
  onComplete: (index: number) => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function SetupStepGuide({ steps, onComplete }: SetupStepGuideProps) {
  const currentStepIndex = steps.findIndex(s => !s.completed);
  const allCompleted = currentStepIndex === -1;

  return (
    <div className="space-y-3">
      {steps.map((step, index) => {
        const isCurrent = index === currentStepIndex;

        return (
          <div
            key={`step-${step.order}`}
            className={[
              'rounded-xl border p-3 transition-shadow',
              step.completed
                ? 'bg-amber-50 border-amber-300'
                : isCurrent
                  ? 'bg-white border-amber-400 shadow-md'
                  : 'bg-white/70 backdrop-blur-md border-white/40 opacity-60',
            ].join(' ')}
          >
            <div className="flex items-start gap-3">
              {/* Step indicator */}
              <div className="shrink-0 mt-0.5">
                {step.completed ? (
                  <CheckCircle className="h-5 w-5 text-amber-500" />
                ) : (
                  <span
                    className={[
                      'flex h-5 w-5 items-center justify-center rounded-full text-xs font-quicksand font-bold',
                      isCurrent ? 'bg-amber-500 text-white' : 'bg-gray-200 text-gray-500',
                    ].join(' ')}
                  >
                    {step.order}
                  </span>
                )}
              </div>

              {/* Step content */}
              <div className="flex-1 min-w-0">
                <p
                  className={[
                    'text-sm font-nunito',
                    step.completed ? 'text-gray-500 line-through' : 'text-gray-900',
                  ].join(' ')}
                >
                  {step.instruction}
                </p>

                {isCurrent && (
                  <Button
                    size="sm"
                    className="mt-2 bg-amber-500 hover:bg-amber-600 text-white font-nunito"
                    onClick={() => onComplete(index)}
                  >
                    Done, next step
                  </Button>
                )}
              </div>
            </div>
          </div>
        );
      })}

      {allCompleted && (
        <div className="rounded-xl bg-amber-50 border border-amber-300 p-4 text-center">
          <p className="text-sm font-quicksand font-semibold text-amber-700">
            All steps completed!
          </p>
        </div>
      )}
    </div>
  );
}
