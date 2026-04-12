'use client';

/**
 * TimerTab — Countdown timer tab for the Default Game Toolkit.
 *
 * State lives in the module-level `toolkit-timer-store` (survives drawer
 * close/reopen). Sound and diary logging are handled in ToolkitDrawerInner
 * (always mounted while the drawer is open) so they fire even when the user
 * is on a different tab.
 *
 * Layout:
 *   [Preset row: 30s 1m 2m 3m 5m 10m]
 *   [Circular countdown ring]
 *   [−1m]  [▶/⏸/↺]  [+1m]
 *   [Reset]
 *   [☑ Reset automatico al cambio turno]
 */

import React, { useCallback } from 'react';

import { cn } from '@/lib/utils';
import { getTimerStore } from '@/stores/toolkit-timer-store';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { TimerDisplay } from './TimerDisplay';
import { TimerPresetRow } from './TimerPresetRow';

// ============================================================================
// Component
// ============================================================================

export function TimerTab() {
  const { gameId } = useToolkitDrawer();
  const timerStore = getTimerStore(gameId);

  const remaining = timerStore(s => s.remaining);
  const total = timerStore(s => s.totalSeconds);
  const status = timerStore(s => s.status);
  const autoReset = timerStore(s => s.autoResetOnTurn);

  const handlePreset = useCallback(
    (seconds: number) => timerStore.getState().setDuration(seconds),
    [timerStore]
  );

  const handlePlayPause = useCallback(() => {
    const s = timerStore.getState();
    if (s.status === 'running') {
      s.pause();
    } else if (s.status === 'ended') {
      s.reset(); // first click resets; user presses play again to start
    } else {
      s.start();
    }
  }, [timerStore]);

  const handleReset = useCallback(() => timerStore.getState().reset(), [timerStore]);

  const handleMinus = useCallback(() => timerStore.getState().adjustTime(-60), [timerStore]);
  const handlePlus = useCallback(() => timerStore.getState().adjustTime(60), [timerStore]);

  const handleAutoReset = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) =>
      timerStore.getState().setAutoResetOnTurn(e.target.checked),
    [timerStore]
  );

  const isPlaying = status === 'running';
  const isEnded = status === 'ended';

  return (
    <div className="flex flex-col items-center gap-5 py-2" data-testid="timer-tab">
      {/* Preset row */}
      <TimerPresetRow selected={total} onSelect={handlePreset} />

      {/* Circular ring display */}
      <TimerDisplay remaining={remaining} total={total} status={status} />

      {/* Adjust ± 1 minute + main control */}
      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={handleMinus}
          disabled={remaining <= 0}
          className="w-12 rounded-xl bg-gray-100 py-2 text-sm font-bold text-gray-600 hover:bg-gray-200 disabled:cursor-not-allowed disabled:opacity-40 transition-colors"
          aria-label="Rimuovi 1 minuto"
          data-testid="timer-minus-1m"
        >
          −1m
        </button>

        {/* Play / Pause / Restart */}
        <button
          type="button"
          onClick={handlePlayPause}
          className={cn(
            'flex h-16 w-16 items-center justify-center rounded-full text-2xl font-bold shadow-md transition-all active:scale-95',
            isEnded
              ? 'bg-red-500 text-white hover:bg-red-600'
              : 'bg-[hsl(142,70%,45%)] text-white hover:bg-[hsl(142,70%,40%)]'
          )}
          aria-label={isEnded ? 'Ricomincia' : isPlaying ? 'Pausa' : 'Avvia'}
          data-testid="timer-play-pause"
        >
          {isEnded ? '↺' : isPlaying ? '⏸' : '▶'}
        </button>

        <button
          type="button"
          onClick={handlePlus}
          className="w-12 rounded-xl bg-gray-100 py-2 text-sm font-bold text-gray-600 hover:bg-gray-200 transition-colors"
          aria-label="Aggiungi 1 minuto"
          data-testid="timer-plus-1m"
        >
          +1m
        </button>
      </div>

      {/* Reset link */}
      <button
        type="button"
        onClick={handleReset}
        className="text-xs text-gray-400 underline-offset-2 hover:text-gray-600 hover:underline"
        data-testid="timer-reset"
      >
        Reset
      </button>

      {/* Auto-reset on turn toggle */}
      <label className="flex cursor-pointer items-center gap-2 rounded-xl border border-gray-200 bg-gray-50 px-4 py-2.5">
        <input
          type="checkbox"
          checked={autoReset}
          onChange={handleAutoReset}
          className="h-4 w-4 accent-[hsl(142,70%,45%)]"
          data-testid="timer-auto-reset-checkbox"
        />
        <span className="text-xs text-gray-600">Reset automatico al cambio turno</span>
      </label>
    </div>
  );
}
