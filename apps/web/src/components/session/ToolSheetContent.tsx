'use client';

import { useState, useEffect, useRef } from 'react';

import type { ToolId } from '@/components/session/QuickToolBar';
import { cn } from '@/lib/utils';

// ── Dadi ─────────────────────────────────────────────────────────────────────

function DadoTool() {
  const [value, setValue] = useState<number | null>(null);
  const [rolling, setRolling] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, []);

  const FACE = ['⚀', '⚁', '⚂', '⚃', '⚄', '⚅'] as const;

  const roll = () => {
    setRolling(true);
    timeoutRef.current = setTimeout(() => {
      setValue(Math.floor(Math.random() * 6) + 1);
      setRolling(false);
    }, 400);
  };

  return (
    <div className="flex flex-col items-center gap-6 py-4">
      <span className={cn('text-8xl select-none', rolling && 'animate-bounce')} aria-hidden="true">
        {value !== null ? FACE[value - 1] : '🎲'}
      </span>
      {value !== null && (
        <p className="text-2xl font-bold tabular-nums" aria-live="polite">
          {value}
        </p>
      )}
      <button
        type="button"
        onClick={roll}
        disabled={rolling}
        aria-label={value === null ? 'Lancia dado' : 'Rilancia dado'}
        className="px-6 py-2 rounded-xl bg-amber-600 text-white text-sm font-semibold disabled:opacity-60"
      >
        {value === null ? 'Lancia' : 'Rilancia'}
      </button>
    </div>
  );
}

// ── Moneta ────────────────────────────────────────────────────────────────────

function MonetaTool() {
  const [result, setResult] = useState<'testa' | 'croce' | null>(null);
  const [flipping, setFlipping] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, []);

  const flip = () => {
    setFlipping(true);
    timeoutRef.current = setTimeout(() => {
      setResult(Math.random() < 0.5 ? 'testa' : 'croce');
      setFlipping(false);
    }, 400);
  };

  return (
    <div className="flex flex-col items-center gap-6 py-4">
      <span className={cn('text-8xl select-none', flipping && 'animate-spin')} aria-hidden="true">
        {result === 'croce' ? '💰' : '🪙'}
      </span>
      {result && (
        <p className="text-xl font-bold capitalize" aria-live="polite">
          {result}
        </p>
      )}
      <button
        type="button"
        onClick={flip}
        disabled={flipping}
        aria-label={result === null ? 'Lancia moneta' : 'Rilancia moneta'}
        className="px-6 py-2 rounded-xl bg-amber-600 text-white text-sm font-semibold disabled:opacity-60"
      >
        {result === null ? 'Lancia' : 'Rilancia'}
      </button>
    </div>
  );
}

// ── Contatore ─────────────────────────────────────────────────────────────────

function ContatoreTool() {
  const [count, setCount] = useState(0);

  return (
    <div className="flex flex-col items-center gap-6 py-4">
      <p className="text-6xl font-bold tabular-nums font-mono" aria-live="polite">
        {count}
      </p>
      <div className="flex gap-4">
        <button
          type="button"
          onClick={() => setCount(c => Math.max(0, c - 1))}
          aria-label="Decrementa"
          className="h-14 w-14 rounded-full bg-white/10 text-3xl font-bold flex items-center justify-center hover:bg-white/20 active:scale-95"
        >
          −
        </button>
        <button
          type="button"
          onClick={() => setCount(c => c + 1)}
          aria-label="Incrementa"
          className="h-14 w-14 rounded-full bg-amber-600 text-3xl font-bold text-white flex items-center justify-center hover:bg-amber-700 active:scale-95"
        >
          +
        </button>
      </div>
      <button
        type="button"
        onClick={() => setCount(0)}
        aria-label="Azzera"
        className="text-xs text-muted-foreground hover:text-foreground underline-offset-2 hover:underline"
      >
        Azzera
      </button>
    </div>
  );
}

// ── Timer ─────────────────────────────────────────────────────────────────────

function TimerTool() {
  const [elapsed, setElapsed] = useState(0);
  const [running, setRunning] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (running) {
      intervalRef.current = setInterval(() => setElapsed(e => e + 1), 1000);
    } else if (intervalRef.current) {
      clearInterval(intervalRef.current);
    }
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [running]);

  const format = (s: number) => {
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
  };

  const reset = () => {
    setRunning(false);
    setElapsed(0);
  };

  return (
    <div className="flex flex-col items-center gap-6 py-4">
      <p
        className="text-5xl font-mono font-bold tabular-nums"
        aria-live="off"
        aria-label={`Timer: ${format(elapsed)}`}
      >
        {format(elapsed)}
      </p>
      <div className="flex gap-3">
        <button
          type="button"
          onClick={() => setRunning(r => !r)}
          aria-label={running ? 'Pausa' : 'Avvia'}
          className="px-6 py-2 rounded-xl bg-amber-600 text-white text-sm font-semibold hover:bg-amber-700"
        >
          {running ? 'Pausa' : 'Avvia'}
        </button>
        <button
          type="button"
          onClick={reset}
          aria-label="Reset"
          className="px-6 py-2 rounded-xl bg-white/10 text-sm font-semibold hover:bg-white/20"
        >
          Reset
        </button>
      </div>
    </div>
  );
}

// ── Main export ───────────────────────────────────────────────────────────────

interface ToolSheetContentProps {
  activeTool: ToolId | null;
}

export function ToolSheetContent({ activeTool }: ToolSheetContentProps) {
  if (activeTool === 'dadi') return <DadoTool />;
  if (activeTool === 'moneta') return <MonetaTool />;
  if (activeTool === 'contatore') return <ContatoreTool />;
  if (activeTool === 'timer') return <TimerTool />;
  if (activeTool === 'carte') {
    return (
      <div className="flex flex-col items-center gap-4 py-8">
        <span className="text-5xl" aria-hidden="true">
          🃏
        </span>
        <p className="text-sm text-muted-foreground">Carte — disponibile prossimamente</p>
      </div>
    );
  }
  return null;
}
