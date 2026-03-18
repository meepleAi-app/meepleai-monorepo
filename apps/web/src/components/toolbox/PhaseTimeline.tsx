'use client';

import React from 'react';

import { ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/button';
import type { PhaseDto } from '@/lib/api/schemas/toolbox.schemas';
import { cn } from '@/lib/utils';

interface PhaseTimelineProps {
  phases: PhaseDto[];
  currentPhaseId: string | null;
  onAdvance: () => void;
  className?: string;
  'data-testid'?: string;
}

/**
 * Horizontal phase stepper showing phase progression with connector lines.
 * Current phase is highlighted with the toolkit entity color (hsl 142 70% 45%).
 * Epic #412 — Game Toolbox.
 */
export function PhaseTimeline({
  phases,
  currentPhaseId,
  onAdvance,
  className = '',
  'data-testid': testId,
}: PhaseTimelineProps) {
  const sortedPhases = [...phases].sort((a, b) => a.order - b.order);
  const currentIndex = sortedPhases.findIndex(p => p.id === currentPhaseId);
  const isLastPhase = currentIndex === sortedPhases.length - 1;

  return (
    <div
      className={cn('flex items-center gap-1 overflow-x-auto px-4 py-3', className)}
      data-testid={testId ?? 'phase-timeline'}
      role="navigation"
      aria-label="Phase progression"
    >
      {sortedPhases.map((phase, index) => {
        const isCurrent = phase.id === currentPhaseId;
        const isPast = currentIndex >= 0 && index < currentIndex;
        const isFuture = currentIndex >= 0 && index > currentIndex;

        return (
          <React.Fragment key={phase.id}>
            {/* Connector line before pill (not on the first pill) */}
            {index > 0 && (
              <div
                className={cn(
                  'h-0.5 w-4 shrink-0',
                  isPast ? 'bg-[hsl(142,70%,45%)]/50' : 'bg-muted'
                )}
                aria-hidden="true"
              />
            )}

            {/* Phase pill */}
            <div
              className={cn(
                'shrink-0 rounded-full px-3 py-1 text-xs font-medium transition-colors',
                isCurrent && 'bg-[hsl(142,70%,45%)] text-white shadow-sm',
                isPast && 'bg-[hsl(142,70%,45%)]/15 text-[hsl(142,70%,35%)]',
                isFuture && 'bg-muted text-muted-foreground',
                !isCurrent && !isPast && !isFuture && 'bg-muted text-muted-foreground'
              )}
              data-testid={`phase-pill-${phase.id}`}
              aria-current={isCurrent ? 'step' : undefined}
            >
              {phase.name}
            </div>
          </React.Fragment>
        );
      })}

      {/* Next Phase button */}
      {sortedPhases.length > 0 && (
        <Button
          variant="ghost"
          size="sm"
          className="ml-2 shrink-0 gap-1 text-xs"
          onClick={onAdvance}
          disabled={isLastPhase}
          data-testid="advance-phase-btn"
        >
          Next Phase
          <ChevronRight className="h-3 w-3" />
        </Button>
      )}
    </div>
  );
}
