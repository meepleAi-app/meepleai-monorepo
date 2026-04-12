'use client';

/**
 * ScoreCell — Editable score cell with tap-to-edit input and +/- stepper.
 */

import React, { useEffect, useRef, useState } from 'react';

import { cn } from '@/lib/utils';

export interface ScoreCellProps {
  value: number;
  onChange: (val: number) => void;
  testId?: string;
}

export function ScoreCell({ value, onChange, testId }: ScoreCellProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(String(value));
  const inputRef = useRef<HTMLInputElement>(null);
  const holdTimerRef = useRef<number | null>(null);
  const holdIntervalRef = useRef<number | null>(null);

  useEffect(() => {
    if (!editing) setDraft(String(value));
  }, [value, editing]);

  useEffect(() => {
    if (editing && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [editing]);

  const commit = () => {
    const parsed = parseInt(draft, 10);
    if (!Number.isNaN(parsed)) {
      onChange(parsed);
    } else {
      setDraft(String(value));
    }
    setEditing(false);
  };

  const cancel = () => {
    setDraft(String(value));
    setEditing(false);
  };

  const stopHold = () => {
    if (holdTimerRef.current !== null) {
      window.clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
    if (holdIntervalRef.current !== null) {
      window.clearInterval(holdIntervalRef.current);
      holdIntervalRef.current = null;
    }
  };

  const startHold = (delta: number) => {
    onChange(value + delta);
    holdTimerRef.current = window.setTimeout(() => {
      let currentDelay = 200;
      const tick = () => {
        onChange(value + delta);
        currentDelay = Math.max(50, currentDelay - 20);
      };
      holdIntervalRef.current = window.setInterval(tick, currentDelay);
    }, 400);
  };

  useEffect(() => {
    return () => stopHold();
  }, []);

  return (
    <div className="flex flex-col items-center gap-0.5" data-testid={testId}>
      {editing ? (
        <input
          ref={inputRef}
          type="number"
          value={draft}
          onChange={e => setDraft(e.target.value)}
          onBlur={commit}
          onKeyDown={e => {
            if (e.key === 'Enter') commit();
            if (e.key === 'Escape') cancel();
          }}
          className="w-12 rounded border border-[hsl(142,70%,45%)] px-1 text-center text-sm font-semibold outline-none"
          data-testid={`${testId}-input`}
        />
      ) : (
        <button
          type="button"
          onClick={() => setEditing(true)}
          className="w-12 rounded px-1 text-center text-sm font-semibold text-gray-800 hover:bg-gray-100"
          data-testid={`${testId}-value`}
        >
          {value}
        </button>
      )}
      <div className="flex items-center gap-0.5">
        <button
          type="button"
          onPointerDown={e => {
            e.preventDefault();
            startHold(-1);
          }}
          onPointerUp={stopHold}
          onPointerLeave={stopHold}
          className={cn(
            'h-5 w-5 rounded text-[10px] font-bold text-gray-500',
            'bg-gray-100 hover:bg-gray-200 active:bg-gray-300'
          )}
          data-testid={`${testId}-dec`}
        >
          −
        </button>
        <button
          type="button"
          onPointerDown={e => {
            e.preventDefault();
            startHold(1);
          }}
          onPointerUp={stopHold}
          onPointerLeave={stopHold}
          className={cn(
            'h-5 w-5 rounded text-[10px] font-bold text-gray-500',
            'bg-gray-100 hover:bg-gray-200 active:bg-gray-300'
          )}
          data-testid={`${testId}-inc`}
        >
          +
        </button>
      </div>
    </div>
  );
}
