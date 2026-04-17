'use client';

/**
 * GameStateDisplay — Renders parsed game state from Vision AI analysis.
 *
 * Session Vision AI — Task 15.1
 */

import { useMemo } from 'react';

import { cn } from '@/lib/utils';

interface ParsedGameState {
  board_description?: string;
  notable_state?: string[];
  confidence?: number;
  [key: string]: unknown;
}

interface GameStateDisplayProps {
  gameStateJson: string;
  className?: string;
}

export function GameStateDisplay({ gameStateJson, className }: GameStateDisplayProps) {
  const parsed = useMemo<ParsedGameState | null>(() => {
    try {
      return JSON.parse(gameStateJson);
    } catch {
      return null;
    }
  }, [gameStateJson]);

  if (!parsed) {
    return (
      <div
        className={cn(
          'rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700',
          className
        )}
      >
        Impossibile interpretare lo stato della partita.
      </div>
    );
  }

  const confidence = typeof parsed.confidence === 'number' ? parsed.confidence : null;

  return (
    <div
      className={cn(
        'space-y-3 rounded-xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-4 shadow-sm',
        className
      )}
    >
      {/* Board description */}
      {parsed.board_description && (
        <div>
          <h4 className="mb-1 font-quicksand text-xs font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
            Stato del tavolo
          </h4>
          <p className="font-nunito text-sm leading-relaxed text-[var(--nh-text-primary)]">
            {parsed.board_description}
          </p>
        </div>
      )}

      {/* Notable state */}
      {parsed.notable_state && parsed.notable_state.length > 0 && (
        <div>
          <h4 className="mb-1.5 font-quicksand text-xs font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
            Elementi rilevanti
          </h4>
          <ul className="space-y-1">
            {parsed.notable_state.map((item, idx) => (
              <li
                key={idx}
                className="flex items-start gap-2 font-nunito text-sm text-[var(--nh-text-primary)]"
              >
                <span className="mt-0.5 text-amber-500">&#8226;</span>
                {item}
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Confidence */}
      {confidence !== null && (
        <div className="flex items-center gap-2 pt-1">
          <div className="h-1.5 flex-1 rounded-full bg-stone-200">
            <div
              className={cn(
                'h-1.5 rounded-full transition-all',
                confidence >= 0.8
                  ? 'bg-green-500'
                  : confidence >= 0.5
                    ? 'bg-amber-500'
                    : 'bg-red-400'
              )}
              style={{ width: `${Math.round(confidence * 100)}%` }}
            />
          </div>
          <span className="font-mono text-xs text-[var(--nh-text-muted)]">
            {Math.round(confidence * 100)}%
          </span>
        </div>
      )}
    </div>
  );
}
