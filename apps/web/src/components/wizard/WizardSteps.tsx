import { Check } from 'lucide-react';

import { cn } from '@/lib/utils';

interface WizardStep {
  id: string;
  label: string;
  description?: string;
}

interface WizardStepsProps {
  steps: WizardStep[];
  currentStep: string;
  onStepClick?: (stepId: string) => void;
  allowSkip?: boolean;
}

/**
 * WizardSteps - Reusable stepper component for multi-step workflows
 *
 * Features:
 * - Accessible (ARIA labels, keyboard navigation)
 * - Responsive design with design system tokens
 * - Visual states: active, completed, pending
 * - Optional step descriptions
 */
export function WizardSteps({
  steps,
  currentStep,
  onStepClick,
  allowSkip = false,
}: WizardStepsProps) {
  const currentIndex = steps.findIndex(s => s.id === currentStep);

  return (
    <nav aria-label="Progress" className="mb-8">
      <ol className="flex items-center justify-between">
        {steps.map((step, index) => {
          const isActive = step.id === currentStep;
          const isComplete = index < currentIndex;
          const isClickable = allowSkip && onStepClick && (isComplete || isActive);

          return (
            <li key={step.id} className="flex-1 text-center">
              <button
                type="button"
                onClick={() => isClickable && onStepClick(step.id)}
                disabled={!isClickable}
                className={cn(
                  'w-full flex flex-col items-center',
                  isClickable && 'cursor-pointer hover:opacity-80',
                  !isClickable && 'cursor-default'
                )}
                aria-current={isActive ? 'step' : undefined}
                aria-label={`Step ${index + 1}: ${step.label}`}
              >
                <div
                  className={cn(
                    'w-10 h-10 rounded-full mx-auto mb-2 flex items-center justify-center font-semibold transition-colors',
                    isActive && 'bg-primary text-primary-foreground ring-2 ring-ring ring-offset-2',
                    isComplete && 'bg-green-600 text-white',
                    !isActive && !isComplete && 'bg-muted text-muted-foreground'
                  )}
                >
                  {isComplete ? <Check className="w-5 h-5" aria-hidden="true" /> : index + 1}
                </div>
                <div className="text-sm font-medium">{step.label}</div>
                {step.description && (
                  <div className="text-xs text-muted-foreground mt-1">{step.description}</div>
                )}
              </button>

              {/* Connector line */}
              {index < steps.length - 1 && (
                <div
                  className={cn('h-0.5 w-full mt-5', isComplete ? 'bg-green-600' : 'bg-muted')}
                  aria-hidden="true"
                />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
