'use client';

import { useState, useEffect, useRef, useCallback } from 'react';

import { Button } from '@/components/ui/primitives/button';

interface TimerProps {
  name: string;
  defaultSeconds: number;
  type: 'countdown' | 'countup' | 'turn';
  onAction?: (action: string, seconds: number) => void;
}

// ── Local timer strategy ────────────────────────────────────────────

function useLocalTimer(defaultSeconds: number) {
  const [seconds, setSeconds] = useState(defaultSeconds);
  const [running, setRunning] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const start = useCallback(() => {
    setRunning(true);
  }, []);

  const pause = useCallback(() => {
    setRunning(false);
  }, []);

  const reset = useCallback(() => {
    setRunning(false);
    setSeconds(defaultSeconds);
  }, [defaultSeconds]);

  useEffect(() => {
    if (!running) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      return;
    }
    intervalRef.current = setInterval(() => {
      setSeconds(s => Math.max(0, s - 1));
    }, 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [running]);

  // Auto-stop at 0 (countdown)
  useEffect(() => {
    if (seconds === 0 && running) {
      setRunning(false);
    }
  }, [seconds, running]);

  return { seconds, running, start, pause, reset };
}

// ── Component ───────────────────────────────────────────────────────

export function Timer({ name, defaultSeconds, type, onAction }: TimerProps) {
  // Phase 1: always use local timer (standalone and in-session).
  // Phase 2: when in-session, SSE timer_tick events will drive the display.
  const { seconds, running, start, pause, reset } = useLocalTimer(defaultSeconds);

  const warningThreshold = 10;
  const isWarning = type === 'countdown' && seconds <= warningThreshold && seconds > 0;
  const isExpired = type === 'countdown' && seconds === 0;

  const handleStart = () => {
    start();
    onAction?.('start', seconds);
  };

  const handlePause = () => {
    pause();
    onAction?.('pause', seconds);
  };

  const handleReset = () => {
    reset();
    onAction?.('reset', defaultSeconds);
  };

  const formatTime = (s: number) => {
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
  };

  return (
    <div className="space-y-3 rounded-lg border border-slate-200 bg-white p-4">
      <div className="text-center text-sm font-semibold text-slate-700">{name}</div>
      <div
        className={`text-center text-5xl font-bold tabular-nums transition-colors ${
          isWarning ? 'text-red-500' : isExpired ? 'text-slate-300' : 'text-slate-800'
        }`}
        data-testid="timer-display"
      >
        {formatTime(seconds)}
      </div>
      {isExpired && (
        <p className="text-center text-xs font-medium text-red-500">⏰ Tempo scaduto!</p>
      )}
      <div className="flex gap-2">
        {!running ? (
          <Button onClick={handleStart} className="flex-1" aria-label="Avvia timer">
            ▶ Avvia
          </Button>
        ) : (
          <Button
            onClick={handlePause}
            variant="outline"
            className="flex-1"
            aria-label="Pausa timer"
          >
            ⏸ Pausa
          </Button>
        )}
        <Button variant="outline" onClick={handleReset} aria-label="Reset timer">
          ↩ Reset
        </Button>
      </div>
    </div>
  );
}
