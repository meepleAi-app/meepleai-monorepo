'use client';

import type { WizardStep } from '@/lib/stores/add-game-wizard-store';
import { cn } from '@/lib/utils';

interface StepIndicatorProps {
  currentStep: WizardStep;
  steps: { label: string; description?: string }[];
}

/**
 * Step indicator bar for the collection wizard.
 * Issue #4818: AddGameSheet Drawer + State Machine
 */
export function StepIndicator({ currentStep, steps }: StepIndicatorProps) {
  return (
    <div className="flex items-center gap-2 px-1">
      {steps.map((step, index) => {
        const stepNumber = (index + 1) as WizardStep;
        const isActive = stepNumber === currentStep;
        const isCompleted = stepNumber < currentStep;

        return (
          <div key={step.label} className="flex items-center gap-2 flex-1">
            <div className="flex items-center gap-2 min-w-0">
              <div
                className={cn(
                  'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors',
                  isCompleted && 'bg-teal-500/20 text-teal-400',
                  isActive && 'bg-teal-500 text-white',
                  !isActive && !isCompleted && 'bg-slate-800 text-slate-500'
                )}
              >
                {isCompleted ? '✓' : stepNumber}
              </div>
              <span
                className={cn(
                  'text-xs font-medium truncate transition-colors',
                  isActive && 'text-slate-200',
                  !isActive && 'text-slate-500'
                )}
              >
                {step.label}
              </span>
            </div>
            {index < steps.length - 1 && (
              <div
                className={cn(
                  'h-px flex-1 transition-colors',
                  isCompleted ? 'bg-teal-500/40' : 'bg-slate-700'
                )}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}
