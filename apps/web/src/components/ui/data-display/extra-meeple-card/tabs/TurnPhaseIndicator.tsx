'use client';

/**
 * TurnPhaseIndicator - Horizontal phase bar showing current turn phase
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import React from 'react';

import { GitBranch } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { TurnPhaseState } from '../types';

interface TurnPhaseIndicatorProps {
  state?: TurnPhaseState;
}

/** Phase step colors based on position relative to current */
const PHASE_COLORS = [
  { bg: 'bg-indigo-500', text: 'text-white', border: 'border-indigo-500' },
  { bg: 'bg-emerald-500', text: 'text-white', border: 'border-emerald-500' },
  { bg: 'bg-amber-500', text: 'text-white', border: 'border-amber-500' },
  { bg: 'bg-rose-500', text: 'text-white', border: 'border-rose-500' },
  { bg: 'bg-violet-500', text: 'text-white', border: 'border-violet-500' },
  { bg: 'bg-teal-500', text: 'text-white', border: 'border-teal-500' },
];

export function TurnPhaseIndicator({ state }: TurnPhaseIndicatorProps) {
  if (!state || state.phases.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2" data-testid="turn-phase-indicator">
      {/* Turn info header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <GitBranch className="h-3.5 w-3.5 text-indigo-500" />
          <span className="font-nunito text-xs font-bold text-slate-700">
            Turn {state.currentTurnNumber}
          </span>
          {state.totalTurns != null && (
            <span className="font-nunito text-[10px] text-slate-400">of {state.totalTurns}</span>
          )}
        </div>
        <span className="font-nunito text-[10px] font-semibold text-indigo-600">
          {state.phases[state.currentPhaseIndex]}
        </span>
      </div>

      {/* Phase bar */}
      <div
        className="flex gap-1"
        role="progressbar"
        aria-valuenow={state.currentPhaseIndex + 1}
        aria-valuemin={1}
        aria-valuemax={state.phases.length}
      >
        {state.phases.map((phase, index) => {
          const colorIndex = index % PHASE_COLORS.length;
          const color = PHASE_COLORS[colorIndex];
          const isCurrent = index === state.currentPhaseIndex;
          const isCompleted = index < state.currentPhaseIndex;
          const isFuture = index > state.currentPhaseIndex;

          return (
            <div
              key={`${phase}-${index}`}
              className="flex-1 min-w-0"
              data-testid={`phase-step-${index}`}
            >
              {/* Phase bar segment */}
              <div
                className={cn(
                  'h-2 rounded-full transition-all duration-300',
                  isCurrent &&
                    cn(color.bg, 'ring-2 ring-offset-1', color.border.replace('border-', 'ring-')),
                  isCompleted && cn(color.bg, 'opacity-60'),
                  isFuture && 'bg-slate-200'
                )}
              />
              {/* Phase label */}
              <p
                className={cn(
                  'mt-1 font-nunito text-[10px] truncate text-center',
                  isCurrent && 'font-bold text-slate-700',
                  isCompleted && 'font-medium text-slate-500',
                  isFuture && 'text-slate-400'
                )}
              >
                {phase}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
