'use client';

import type { JSX } from 'react';

import { BookOpen } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { useTranslation } from '@/hooks/useTranslation';
import type { SetupGuideResponseStep } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export interface SetupStepCardProps {
  step: SetupGuideResponseStep;
  isCompleted: boolean;
  onToggleComplete: () => void;
  onViewReferences: () => void;
}

/**
 * Single setup-guide step: checkbox + title + instruction + optional
 * references CTA. Issue: fix/setup-page-redesign.
 */
export function SetupStepCard({
  step,
  isCompleted,
  onToggleComplete,
  onViewReferences,
}: SetupStepCardProps): JSX.Element {
  const { t } = useTranslation();
  const referenceCount = step.references.length;

  return (
    <div
      className={cn(
        'rounded-lg border border-border bg-card p-5 shadow-sm transition-colors',
        isCompleted && 'border-entity-toolkit/30 bg-entity-toolkit/[0.06]'
      )}
    >
      <div className="flex items-start gap-4">
        <Checkbox
          checked={isCompleted}
          onCheckedChange={onToggleComplete}
          className="mt-1"
          aria-label={`${step.stepNumber}. ${step.title} — ${
            isCompleted ? t('pages.setup.step.markIncomplete') : t('pages.setup.step.markComplete')
          }`}
        />

        <div className="min-w-0 flex-1">
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <span className="text-base font-semibold text-foreground">
              {step.stepNumber}. {step.title}
            </span>
            {step.isOptional && (
              <Badge variant="outline" className="text-[11px]">
                {t('pages.setup.optional')}
              </Badge>
            )}
          </div>

          <p
            className={cn(
              'mb-3 text-sm leading-relaxed text-muted-foreground',
              isCompleted && 'line-through opacity-70'
            )}
          >
            {step.instruction}
          </p>

          {referenceCount > 0 && (
            <button
              type="button"
              onClick={onViewReferences}
              className="inline-flex items-center gap-1.5 rounded border border-primary px-3 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/10"
            >
              <BookOpen className="h-3.5 w-3.5" aria-hidden="true" />
              {t('pages.setup.viewReferences', { n: referenceCount })}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
