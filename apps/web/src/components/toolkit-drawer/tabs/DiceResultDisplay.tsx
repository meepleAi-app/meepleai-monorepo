'use client';

/**
 * DiceResultDisplay — Shows the result of the last dice roll
 * with individual die values and a prominent total.
 */

import { cn } from '@/lib/utils';

import type { DiceResult } from '../types';

export interface DiceResultDisplayProps {
  result: DiceResult | null;
  isRolling: boolean;
}

export function DiceResultDisplay({ result, isRolling }: DiceResultDisplayProps) {
  if (!result) return null;

  return (
    <div
      className={cn(
        'flex flex-col items-center gap-2 rounded-xl bg-gray-50 p-4 transition-transform',
        isRolling && 'animate-dice-shake'
      )}
      data-testid="dice-result-display"
    >
      {/* Individual rolls */}
      <div className="flex flex-wrap justify-center gap-1.5">
        {result.rolls.map((value, i) => (
          <span
            key={i}
            className="flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 bg-white text-sm font-semibold text-gray-700"
          >
            {value}
          </span>
        ))}
        {result.modifier !== 0 && (
          <span className="flex h-8 items-center px-1 text-sm font-medium text-gray-500">
            {result.modifier > 0 ? `+${result.modifier}` : result.modifier}
          </span>
        )}
      </div>

      {/* Total */}
      <div className="text-3xl font-bold text-gray-900" data-testid="dice-result-total">
        {result.total}
      </div>

      {/* Formula label */}
      <div className="text-xs text-gray-400">{result.formula}</div>

      {/* Inline keyframes for the shake animation */}
      <style jsx>{`
        @keyframes dice-shake {
          0%,
          100% {
            transform: translateX(0);
          }
          10%,
          50%,
          90% {
            transform: translateX(-2px) rotate(-1deg);
          }
          30%,
          70% {
            transform: translateX(2px) rotate(1deg);
          }
        }
        :global(.animate-dice-shake) {
          animation: dice-shake 0.3s ease-in-out;
        }
      `}</style>
    </div>
  );
}
