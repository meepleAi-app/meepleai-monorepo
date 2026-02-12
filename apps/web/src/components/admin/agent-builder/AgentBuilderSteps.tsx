/**
 * ISSUE-3709: Agent Builder - Step Indicator
 * Visual step progress indicator for multi-step wizard
 */

'use client';

import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

interface Step {
  number: number;
  title: string;
  description: string;
}

const STEPS: Step[] = [
  { number: 1, title: 'Basic Info', description: 'Agent details and configuration' },
  { number: 2, title: 'System Prompts', description: 'Define agent behavior and personality' },
  { number: 3, title: 'Tools & Strategy', description: 'Select tools and retrieval strategy' },
  { number: 4, title: 'Review', description: 'Verify and create agent' },
];

interface AgentBuilderStepsProps {
  currentStep: number;
}

export function AgentBuilderSteps({ currentStep }: AgentBuilderStepsProps) {
  return (
    <nav aria-label="Progress" className="mb-8">
      <ol role="list" className="flex items-center justify-between">
        {STEPS.map((step, stepIdx) => (
          <li
            key={step.number}
            className={cn(
              'relative',
              stepIdx !== STEPS.length - 1 ? 'pr-8 sm:pr-20 flex-1' : ''
            )}
          >
            {/* Connector Line */}
            {stepIdx !== STEPS.length - 1 && (
              <div
                className="absolute top-4 left-0 -ml-px mt-0.5 h-0.5 w-full"
                aria-hidden="true"
              >
                <div
                  className={cn(
                    'h-full transition-colors',
                    currentStep > step.number ? 'bg-primary' : 'bg-muted'
                  )}
                />
              </div>
            )}

            {/* Step Content */}
            <div className="group relative flex items-start">
              <span className="flex h-9 items-center">
                <span
                  className={cn(
                    'relative z-10 flex h-8 w-8 items-center justify-center rounded-full transition-colors',
                    currentStep > step.number
                      ? 'bg-primary text-primary-foreground'
                      : currentStep === step.number
                        ? 'border-2 border-primary bg-background text-primary'
                        : 'border-2 border-muted bg-background text-muted-foreground'
                  )}
                >
                  {currentStep > step.number ? (
                    <Check className="h-5 w-5" />
                  ) : (
                    <span className="text-sm font-semibold">{step.number}</span>
                  )}
                </span>
              </span>
              <span className="ml-4 flex min-w-0 flex-col">
                <span
                  className={cn(
                    'text-sm font-medium transition-colors',
                    currentStep >= step.number ? 'text-foreground' : 'text-muted-foreground'
                  )}
                >
                  {step.title}
                </span>
                <span className="text-xs text-muted-foreground hidden sm:block">
                  {step.description}
                </span>
              </span>
            </div>
          </li>
        ))}
      </ol>
    </nav>
  );
}
