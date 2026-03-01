'use client';

/**
 * CounterTool — Game session counter component (Issue #4979).
 *
 * Supports two modes driven by CounterToolConfig.isPerPlayer:
 * - Per-player: each participant has an independent counter row.
 * - Shared:     a single counter tracked for the whole session.
 *
 * Features:
 * - +/- buttons with configurable step (default: 1)
 * - Long-press for rapid increment/decrement (x10 step)
 * - Clamps within [minValue, maxValue]
 * - Optimistic UI via useCounterTool hook
 * - Custom icon + color from CounterToolConfig
 */

import React, { useState, useCallback, useRef, useEffect } from 'react';

import { Hash, Loader2, RefreshCw } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { CounterToolConfig, CounterState, Participant } from './types';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface CounterToolProps {
  /** Counter configuration from the game toolkit. */
  config: CounterToolConfig;
  /** Current counter state (null = not yet loaded). */
  counterState: CounterState | null;
  /** Session participants (used in per-player mode). */
  participants: Participant[];
  /** The current user's participant ID. */
  currentUserId: string;
  /** Whether an API call is in flight. */
  isPending?: boolean;
  /** Last error from the hook, shown in the UI. */
  error?: string | null;
  /** Called when +/- is pressed; parent handles optimistic update. */
  onApplyChange: (playerId: string, change: number) => Promise<void>;
  className?: string;
}

// ── Constants ─────────────────────────────────────────────────────────────────

/** Default step for +/- buttons. */
const DEFAULT_STEP = 1;
/** Long-press multiplier. */
const LONG_PRESS_MULTIPLIER = 10;
/** Long-press activation delay in ms. */
const LONG_PRESS_DELAY_MS = 500;
/** Long-press repeat interval in ms. */
const LONG_PRESS_INTERVAL_MS = 120;

// ── Sub-component: CounterRow ─────────────────────────────────────────────────

interface CounterRowProps {
  label: string;
  value: number;
  minValue: number;
  maxValue: number;
  step: number;
  isPending: boolean;
  accentColor?: string | null;
  onDecrement: () => void;
  onIncrement: () => void;
  onLongPressDecrement: () => void;
  onLongPressIncrement: () => void;
  onLongPressEnd: () => void;
}

function CounterRow({
  label,
  value,
  minValue,
  maxValue,
  step,
  isPending,
  accentColor,
  onDecrement,
  onIncrement,
  onLongPressDecrement,
  onLongPressIncrement,
  onLongPressEnd,
}: CounterRowProps) {
  const decrementDisabled = isPending || value <= minValue;
  const incrementDisabled = isPending || value >= maxValue;

  // Long-press state
  const longPressTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const longPressInterval = useRef<ReturnType<typeof setInterval> | null>(null);

  const startLongPress = useCallback(
    (fn: () => void) => {
      longPressTimer.current = setTimeout(() => {
        fn();
        longPressInterval.current = setInterval(fn, LONG_PRESS_INTERVAL_MS);
      }, LONG_PRESS_DELAY_MS);
    },
    []
  );

  const endLongPress = useCallback(() => {
    if (longPressTimer.current !== null) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
    if (longPressInterval.current !== null) {
      clearInterval(longPressInterval.current);
      longPressInterval.current = null;
    }
    onLongPressEnd();
  }, [onLongPressEnd]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (longPressTimer.current !== null) clearTimeout(longPressTimer.current);
      if (longPressInterval.current !== null) clearInterval(longPressInterval.current);
    };
  }, []);

  const btnBase = cn(
    'w-9 h-9 flex items-center justify-center rounded-lg text-lg font-bold',
    'transition-colors duration-100 select-none touch-none',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-1',
    'disabled:opacity-40 disabled:cursor-not-allowed',
    'bg-stone-100 dark:bg-stone-800 hover:bg-stone-200 dark:hover:bg-stone-700',
    'border border-stone-200 dark:border-stone-700'
  );

  return (
    <div className="flex items-center gap-3 px-2 py-1.5">
      {/* Player label */}
      <span className="flex-1 text-sm font-medium text-stone-700 dark:text-stone-300 truncate">
        {label}
      </span>

      {/* Decrement */}
      <button
        type="button"
        aria-label={`Decrement ${label} by ${step}`}
        disabled={decrementDisabled}
        className={btnBase}
        onClick={onDecrement}
        onPointerDown={() => !decrementDisabled && startLongPress(onLongPressDecrement)}
        onPointerUp={endLongPress}
        onPointerLeave={endLongPress}
      >
        −
      </button>

      {/* Value display */}
      <span
        className={cn(
          'w-16 text-center text-xl font-bold tabular-nums',
          'text-stone-900 dark:text-stone-100',
          isPending && 'opacity-60'
        )}
        aria-live="polite"
        aria-atomic="true"
        aria-label={`${label}: ${value}`}
        style={accentColor ? { color: accentColor } : undefined}
      >
        {value}
      </span>

      {/* Increment */}
      <button
        type="button"
        aria-label={`Increment ${label} by ${step}`}
        disabled={incrementDisabled}
        className={btnBase}
        onClick={onIncrement}
        onPointerDown={() => !incrementDisabled && startLongPress(onLongPressIncrement)}
        onPointerUp={endLongPress}
        onPointerLeave={endLongPress}
      >
        +
      </button>
    </div>
  );
}

// ── Main Component ────────────────────────────────────────────────────────────

/**
 * CounterTool
 *
 * Rendered when `activeTool === 'counter'` (or a specific counter tool name)
 * in the session toolkit page.
 */
export function CounterTool({
  config,
  counterState,
  participants,
  currentUserId,
  isPending = false,
  error = null,
  onApplyChange,
  className,
}: CounterToolProps) {
  const [isResetting, setIsResetting] = useState(false);

  const step = DEFAULT_STEP;
  const rapidStep = step * LONG_PRESS_MULTIPLIER;

  // ── Handlers ────────────────────────────────────────────────────────────────

  const handleChange = useCallback(
    async (playerId: string, change: number) => {
      await onApplyChange(playerId, change);
    },
    [onApplyChange]
  );

  const handleReset = useCallback(async () => {
    if (!counterState || isResetting) return;

    setIsResetting(true);
    try {
      if (config.isPerPlayer) {
        // Reset all players to defaultValue
        await Promise.all(
          participants.map(p => {
            const current = counterState.playerValues[p.id] ?? config.defaultValue;
            const change = config.defaultValue - current;
            return change !== 0 ? onApplyChange(p.id, change) : Promise.resolve();
          })
        );
      } else {
        const change = config.defaultValue - counterState.currentValue;
        if (change !== 0) await onApplyChange(currentUserId, change);
      }
    } finally {
      setIsResetting(false);
    }
  }, [counterState, isResetting, config, participants, currentUserId, onApplyChange]);

  // ── Loading state ────────────────────────────────────────────────────────────

  if (!counterState) {
    return (
      <div
        className="flex items-center justify-center min-h-[200px]"
        aria-busy="true"
      >
        <Loader2 className="w-6 h-6 animate-spin text-amber-600" aria-hidden="true" />
        <span className="sr-only">Caricamento contatore…</span>
      </div>
    );
  }

  // ── Shared single value ──────────────────────────────────────────────────────

  const sharedPlayerId =
    config.isPerPlayer ? null : (participants.find(p => p.isCurrentUser)?.id ?? currentUserId);

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <section
      className={cn('flex flex-col gap-3 max-w-md mx-auto py-4', className)}
      aria-label={`Contatore: ${config.name}`}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-2">
        <h2 className="flex items-center gap-2 text-base font-semibold text-stone-800 dark:text-stone-200">
          {config.icon ? (
            <span aria-hidden="true">{config.icon}</span>
          ) : (
            <Hash
              className="w-4 h-4 text-amber-600 dark:text-amber-400"
              aria-hidden="true"
            />
          )}
          {config.name}
        </h2>

        {/* Mode badge */}
        <span className="px-2 py-0.5 text-xs font-semibold rounded-full bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400 border border-stone-200 dark:border-stone-700">
          {config.isPerPlayer ? 'Per player' : 'Condiviso'}
        </span>
      </div>

      {/* Error */}
      {error && (
        <p
          role="alert"
          aria-live="assertive"
          className="px-2 text-sm text-red-600 dark:text-red-400"
        >
          {error}
        </p>
      )}

      {/* Counter rows */}
      <div
        className="flex flex-col divide-y divide-stone-100 dark:divide-stone-800 rounded-xl border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-900"
        aria-label="Contatori"
      >
        {config.isPerPlayer ? (
          participants.map(participant => {
            const value = counterState.playerValues[participant.id] ?? config.defaultValue;
            return (
              <CounterRow
                key={participant.id}
                label={participant.displayName}
                value={value}
                minValue={config.minValue}
                maxValue={config.maxValue}
                step={step}
                isPending={isPending}
                accentColor={config.color}
                onDecrement={() => void handleChange(participant.id, -step)}
                onIncrement={() => void handleChange(participant.id, step)}
                onLongPressDecrement={() => void handleChange(participant.id, -rapidStep)}
                onLongPressIncrement={() => void handleChange(participant.id, rapidStep)}
                onLongPressEnd={() => {/* handled in CounterRow */}}
              />
            );
          })
        ) : (
          <CounterRow
            label={config.name}
            value={counterState.currentValue}
            minValue={config.minValue}
            maxValue={config.maxValue}
            step={step}
            isPending={isPending}
            accentColor={config.color}
            onDecrement={() => sharedPlayerId && void handleChange(sharedPlayerId, -step)}
            onIncrement={() => sharedPlayerId && void handleChange(sharedPlayerId, step)}
            onLongPressDecrement={() =>
              sharedPlayerId && void handleChange(sharedPlayerId, -rapidStep)
            }
            onLongPressIncrement={() =>
              sharedPlayerId && void handleChange(sharedPlayerId, rapidStep)
            }
            onLongPressEnd={() => {/* handled in CounterRow */}}
          />
        )}
      </div>

      {/* Range indicator */}
      <p className="px-2 text-xs text-stone-400 dark:text-stone-600 text-center">
        Range: {config.minValue} – {config.maxValue}
        {isPending && (
          <span className="ml-2 inline-flex items-center gap-1">
            <Loader2 className="w-3 h-3 animate-spin" aria-hidden="true" />
            <span className="sr-only">Aggiornamento in corso…</span>
          </span>
        )}
      </p>

      {/* Reset button */}
      <div className="flex justify-center pt-1">
        <button
          type="button"
          onClick={() => void handleReset()}
          disabled={isResetting || isPending}
          className={cn(
            'flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium',
            'text-stone-600 dark:text-stone-400',
            'border border-stone-300 dark:border-stone-600',
            'hover:bg-stone-100 dark:hover:bg-stone-800',
            'transition-colors duration-150',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-1',
            'disabled:opacity-50 disabled:cursor-not-allowed'
          )}
          aria-label={`Reset ${config.name} to ${config.defaultValue}`}
        >
          {isResetting ? (
            <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
          ) : (
            <RefreshCw className="w-4 h-4" aria-hidden="true" />
          )}
          Reset a {config.defaultValue}
        </button>
      </div>
    </section>
  );
}
