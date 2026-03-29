'use client';

import React, { useState } from 'react';

import { Delete, Check } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface ScoreNumpadProps {
  playerName: string;
  currentScore?: number;
  onSubmit: (value: number) => void;
  onClose: () => void;
  className?: string;
}

export function ScoreNumpad({
  playerName,
  currentScore,
  onSubmit,
  onClose,
  className,
}: ScoreNumpadProps) {
  const [input, setInput] = useState('');

  const displayValue = input === '' ? '0' : input;

  function handleDigit(digit: string) {
    setInput(prev => {
      if (prev === '' && digit === '0') return '';
      return (prev + digit).slice(0, 6);
    });
  }

  function handleDelete() {
    setInput(prev => prev.slice(0, -1));
  }

  function handleConfirm() {
    const value = input === '' ? 0 : parseInt(input, 10);
    onSubmit(value);
  }

  const KEYS: Array<{ label: string; action: () => void; type: 'digit' | 'delete' | 'confirm' }> = [
    { label: '1', action: () => handleDigit('1'), type: 'digit' },
    { label: '2', action: () => handleDigit('2'), type: 'digit' },
    { label: '3', action: () => handleDigit('3'), type: 'digit' },
    { label: '4', action: () => handleDigit('4'), type: 'digit' },
    { label: '5', action: () => handleDigit('5'), type: 'digit' },
    { label: '6', action: () => handleDigit('6'), type: 'digit' },
    { label: '7', action: () => handleDigit('7'), type: 'digit' },
    { label: '8', action: () => handleDigit('8'), type: 'digit' },
    { label: '9', action: () => handleDigit('9'), type: 'digit' },
    { label: 'delete', action: handleDelete, type: 'delete' },
    { label: '0', action: () => handleDigit('0'), type: 'digit' },
    { label: 'confirm', action: handleConfirm, type: 'confirm' },
  ];

  return (
    <div
      className={cn(
        'flex flex-col items-center gap-4 rounded-2xl bg-[var(--gaming-surface,#1a1a2e)] p-4',
        className
      )}
      aria-label={`Numpad per ${playerName}`}
    >
      {/* Header */}
      <div className="flex w-full items-center justify-between">
        <span className="text-sm font-medium text-[var(--gaming-text-secondary,#ccc)]">
          {playerName}
        </span>
        {currentScore !== undefined && (
          <span className="text-xs text-[var(--gaming-text-secondary,#aaa)]">
            Attuale: {currentScore}
          </span>
        )}
        <button
          aria-label="Chiudi"
          onClick={onClose}
          className="text-[var(--gaming-text-secondary,#ccc)] hover:text-white"
        >
          ✕
        </button>
      </div>

      {/* Display */}
      <div
        aria-live="polite"
        aria-label={`Valore corrente: ${displayValue}`}
        className="text-5xl font-bold tabular-nums text-white"
      >
        {displayValue}
      </div>

      {/* Numpad grid */}
      <div className="grid grid-cols-3 gap-2 w-full">
        {KEYS.map(({ label, action, type }) => {
          if (type === 'delete') {
            return (
              <button
                key="delete"
                aria-label="Cancella"
                onClick={action}
                className="flex items-center justify-center rounded-xl bg-white/10 p-4 text-white hover:bg-white/20 active:scale-95"
              >
                <Delete className="h-5 w-5" aria-hidden="true" />
              </button>
            );
          }

          if (type === 'confirm') {
            return (
              <button
                key="confirm"
                aria-label="Conferma"
                onClick={action}
                className="flex items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 to-amber-600 p-4 text-white shadow-md hover:from-amber-500 hover:to-amber-700 active:scale-95"
              >
                <Check className="h-5 w-5" aria-hidden="true" />
              </button>
            );
          }

          return (
            <button
              key={label}
              aria-label={label}
              onClick={action}
              className="rounded-xl bg-white/10 p-4 text-lg font-semibold text-white hover:bg-white/20 active:scale-95"
            >
              {label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
