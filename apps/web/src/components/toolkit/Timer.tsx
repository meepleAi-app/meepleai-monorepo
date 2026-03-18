'use client';

import React, { useState, useRef, useCallback, useEffect } from 'react';

import { Button } from '@/components/ui/primitives/button';

type TimerMode = 'countdown' | 'stopwatch';

function formatTime(totalSeconds: number): string {
  const m = Math.floor(Math.abs(totalSeconds) / 60);
  const s = Math.abs(totalSeconds) % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

/**
 * Timer — countdown and stopwatch for board game sessions.
 */
export function Timer() {
  const [mode, setMode] = useState<TimerMode>('countdown');
  const [running, setRunning] = useState(false);

  // Countdown state
  const [cdMinutes, setCdMinutes] = useState(5);
  const [cdSeconds, setCdSeconds] = useState(0);
  const [cdRemaining, setCdRemaining] = useState<number | null>(null);

  // Stopwatch state
  const [swElapsed, setSwElapsed] = useState(0);

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const clearTimer = useCallback(() => {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const startCountdown = useCallback(() => {
    const total = cdMinutes * 60 + cdSeconds;
    const startValue = cdRemaining !== null ? cdRemaining : total;
    if (startValue <= 0) return;

    setCdRemaining(startValue);
    setRunning(true);

    intervalRef.current = setInterval(() => {
      setCdRemaining(prev => {
        if (prev === null || prev <= 1) {
          clearTimer();
          setRunning(false);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, [cdMinutes, cdSeconds, cdRemaining, clearTimer]);

  const startStopwatch = useCallback(() => {
    setRunning(true);
    intervalRef.current = setInterval(() => {
      setSwElapsed(prev => prev + 1);
    }, 1000);
  }, []);

  const pause = useCallback(() => {
    clearTimer();
    setRunning(false);
  }, [clearTimer]);

  const resetCountdown = useCallback(() => {
    clearTimer();
    setRunning(false);
    setCdRemaining(null);
  }, [clearTimer]);

  const resetStopwatch = useCallback(() => {
    clearTimer();
    setRunning(false);
    setSwElapsed(0);
  }, [clearTimer]);

  // Cleanup on unmount
  useEffect(() => () => clearTimer(), [clearTimer]);

  // Switch modes resets everything
  const switchMode = (newMode: TimerMode) => {
    clearTimer();
    setRunning(false);
    setCdRemaining(null);
    setSwElapsed(0);
    setMode(newMode);
  };

  const displayTime =
    mode === 'countdown'
      ? formatTime(cdRemaining !== null ? cdRemaining : cdMinutes * 60 + cdSeconds)
      : formatTime(swElapsed);

  const isFinished = mode === 'countdown' && cdRemaining === 0;

  return (
    <div className="space-y-4" data-testid="timer">
      {/* Mode toggle */}
      <div className="flex rounded-md border border-slate-200 overflow-hidden">
        <button
          onClick={() => switchMode('countdown')}
          className={`flex-1 py-1.5 text-sm font-medium transition-colors ${
            mode === 'countdown'
              ? 'bg-amber-500 text-white'
              : 'bg-white text-slate-600 hover:bg-slate-50'
          }`}
          aria-pressed={mode === 'countdown'}
        >
          Countdown
        </button>
        <button
          onClick={() => switchMode('stopwatch')}
          className={`flex-1 py-1.5 text-sm font-medium transition-colors ${
            mode === 'stopwatch'
              ? 'bg-amber-500 text-white'
              : 'bg-white text-slate-600 hover:bg-slate-50'
          }`}
          aria-pressed={mode === 'stopwatch'}
        >
          Cronometro
        </button>
      </div>

      {/* Countdown setup */}
      {mode === 'countdown' && cdRemaining === null && !running && (
        <div className="flex items-center justify-center gap-3">
          <div className="flex flex-col items-center">
            <label className="mb-1 text-xs text-slate-500">Min</label>
            <input
              type="number"
              min={0}
              max={99}
              value={cdMinutes}
              onChange={e => setCdMinutes(Math.min(99, Math.max(0, parseInt(e.target.value) || 0)))}
              className="w-16 rounded-md border border-slate-200 py-1 text-center text-lg font-mono focus:border-amber-400 focus:outline-none"
              aria-label="Minutes"
            />
          </div>
          <span className="mt-4 text-2xl font-bold text-slate-400">:</span>
          <div className="flex flex-col items-center">
            <label className="mb-1 text-xs text-slate-500">Sec</label>
            <input
              type="number"
              min={0}
              max={59}
              value={cdSeconds}
              onChange={e => setCdSeconds(Math.min(59, Math.max(0, parseInt(e.target.value) || 0)))}
              className="w-16 rounded-md border border-slate-200 py-1 text-center text-lg font-mono focus:border-amber-400 focus:outline-none"
              aria-label="Seconds"
            />
          </div>
        </div>
      )}

      {/* Large display */}
      <div
        className={`text-center text-5xl font-bold tabular-nums tracking-tight ${
          isFinished ? 'text-red-500' : 'text-slate-800'
        }`}
        aria-live="polite"
        aria-label={`Time: ${displayTime}`}
        data-testid="timer-display"
      >
        {displayTime}
        {isFinished && <p className="mt-1 text-sm font-normal text-red-500">Tempo scaduto!</p>}
      </div>

      {/* Controls */}
      <div className="flex gap-2">
        {!running ? (
          <Button
            onClick={mode === 'countdown' ? startCountdown : startStopwatch}
            className="flex-1"
            disabled={mode === 'countdown' && cdRemaining === 0}
            aria-label="Start timer"
          >
            {cdRemaining !== null && mode === 'countdown' ? 'Riprendi' : 'Avvia'}
          </Button>
        ) : (
          <Button variant="outline" onClick={pause} className="flex-1" aria-label="Pause timer">
            Pausa
          </Button>
        )}
        <Button
          variant="outline"
          onClick={mode === 'countdown' ? resetCountdown : resetStopwatch}
          aria-label="Reset timer"
        >
          Reset
        </Button>
      </div>
    </div>
  );
}
