/**
 * GamePreferenceSelect — accessible combobox/listbox for game preference.
 *
 * Wave A.2 (Issue #589). Mirrors mockup `sp3-join.jsx` lines 111-269.
 * Spec §3.2 `GamePreferenceSelectProps` + binding a11y contract:
 * - Trigger button: `aria-haspopup="listbox"` + `aria-expanded` + `aria-controls`.
 * - Popover: `role="listbox"` + `aria-activedescendant`.
 * - Each option: `role="option"` + stable `id` + `aria-selected`.
 * - Keyboard: Enter/Space/ArrowDown opens; ArrowDown/Up move active descendant;
 *   Enter selects + closes + focus returns; Escape closes + focus returns;
 *   Tab dismisses naturally (no focus trap).
 * - "Altro" row: top separator + monospace font; when selected, a free-text
 *   input is revealed and auto-focused for the user to specify a custom title.
 * - Click outside: `mousedown` listener with effect cleanup.
 *
 * The component is **purely controlled**: caller owns `value`, `otherText`,
 * and `error` state. Games and labels are injected (no hardcoded strings) so
 * it can be reused in non-join surfaces (e.g. preference editing, post-alpha).
 */

'use client';

import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import type { JSX, KeyboardEvent } from 'react';

import clsx from 'clsx';

import type { GamePreference } from '@/lib/join/games';
import { GAME_OTHER_ID } from '@/lib/join/games';

export interface GamePreferenceSelectLabels {
  readonly placeholder: string;
  readonly otherPlaceholder: string;
  readonly listboxAriaLabel: string;
  readonly otherInputAriaLabel: string;
  readonly fieldLabel: string;
}

export interface GamePreferenceSelectProps {
  readonly value: string;
  readonly onChange: (id: string) => void;
  readonly otherText: string;
  readonly onOtherText: (next: string) => void;
  readonly error?: string;
  readonly games: readonly GamePreference[];
  readonly labels: GamePreferenceSelectLabels;
  readonly disabled?: boolean;
  readonly className?: string;
}

export function GamePreferenceSelect({
  value,
  onChange,
  otherText,
  onOtherText,
  error,
  games,
  labels,
  disabled = false,
  className,
}: GamePreferenceSelectProps): JSX.Element {
  const [open, setOpen] = useState(false);
  const [activeIdx, setActiveIdx] = useState(0);
  const btnRef = useRef<HTMLButtonElement | null>(null);
  const popRef = useRef<HTMLDivElement | null>(null);
  const otherInputRef = useRef<HTMLInputElement | null>(null);

  const reactId = useId();
  const listboxId = `${reactId}-listbox`;
  const labelId = `${reactId}-label`;
  const errorId = `${reactId}-error`;
  const optionId = useCallback((idx: number): string => `${reactId}-option-${idx}`, [reactId]);

  const selected = useMemo(() => games.find(g => g.id === value), [games, value]);
  const isOther = value === GAME_OTHER_ID;

  // Focus the "Altro" input when the user picks the escape hatch.
  useEffect(() => {
    if (isOther) {
      otherInputRef.current?.focus();
    }
  }, [isOther]);

  // Click outside dismisses the listbox.
  useEffect(() => {
    if (!open) return undefined;
    const onDoc = (e: MouseEvent): void => {
      const target = e.target as Node | null;
      if (target && !btnRef.current?.contains(target) && !popRef.current?.contains(target)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  const onKey = (e: KeyboardEvent<HTMLButtonElement>): void => {
    if (!open) {
      if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') {
        e.preventDefault();
        const i = games.findIndex(g => g.id === value);
        setActiveIdx(i >= 0 ? i : 0);
        setOpen(true);
      }
      return;
    }
    if (e.key === 'Escape') {
      e.preventDefault();
      setOpen(false);
      btnRef.current?.focus();
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIdx(i => Math.min(i + 1, games.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIdx(i => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const next = games[activeIdx];
      if (next) {
        onChange(next.id);
      }
      setOpen(false);
      btnRef.current?.focus();
    } else if (e.key === 'Tab') {
      // Natural tab order — close popover but don't preventDefault.
      setOpen(false);
    }
  };

  const handleOptionClick = (id: string): void => {
    onChange(id);
    setOpen(false);
    btnRef.current?.focus();
  };

  return (
    <div data-slot="game-preference-select" className={clsx('mb-3', className)}>
      <label
        id={labelId}
        htmlFor={`${reactId}-button`}
        className="mb-1.5 block font-display text-[10px] font-bold uppercase tracking-[0.06em] text-[hsl(var(--text-sec))]"
      >
        {labels.fieldLabel}
      </label>

      <div className="relative">
        <button
          id={`${reactId}-button`}
          ref={btnRef}
          type="button"
          disabled={disabled}
          onClick={() => setOpen(v => !v)}
          onKeyDown={onKey}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-controls={listboxId}
          // `aria-labelledby` points to the visible <label> — `aria-label` would
          // override it (per ARIA spec) and shadow the visible field label for
          // assistive tech. Rely on the visible label only.
          aria-labelledby={labelId}
          aria-describedby={error ? errorId : undefined}
          aria-activedescendant={open ? optionId(activeIdx) : undefined}
          aria-invalid={Boolean(error)}
          style={{
            borderColor: error ? 'hsl(var(--c-danger))' : undefined,
            backgroundColor: error ? 'hsl(var(--c-danger) / 0.07)' : undefined,
          }}
          className={clsx(
            'flex w-full items-center justify-between',
            'px-3 py-2.5 rounded-md',
            'border-[1.5px] border-border bg-background',
            'text-[13px] text-foreground text-left',
            'transition-colors duration-150 ease-out',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
            disabled && 'cursor-not-allowed opacity-65'
          )}
        >
          <span className="flex items-center gap-2">
            {selected ? (
              <>
                <span aria-hidden="true" className="text-base leading-none">
                  {selected.emoji}
                </span>
                <span>{selected.label}</span>
              </>
            ) : (
              <span className="text-[hsl(var(--text-muted))]">{labels.placeholder}</span>
            )}
          </span>
          <span
            aria-hidden="true"
            className={clsx(
              'font-mono text-[11px] text-[hsl(var(--text-muted))]',
              'transition-transform duration-150',
              open && 'rotate-180'
            )}
          >
            ▾
          </span>
        </button>

        {open && (
          <div
            id={listboxId}
            ref={popRef}
            role="listbox"
            aria-label={labels.listboxAriaLabel}
            className={clsx(
              'absolute left-0 right-0 top-[calc(100%+4px)] z-10',
              'max-h-60 overflow-y-auto p-1',
              'bg-card border border-border rounded-md shadow-lg'
            )}
          >
            {games.map((g, i) => {
              const isSel = g.id === value;
              const isAct = i === activeIdx;
              const isOtherRow = g.id === GAME_OTHER_ID;
              return (
                <div
                  id={optionId(i)}
                  key={g.id}
                  role="option"
                  aria-selected={isSel}
                  data-testid={`game-option-${g.id}`}
                  onClick={() => handleOptionClick(g.id)}
                  onMouseEnter={() => setActiveIdx(i)}
                  className={clsx(
                    'flex cursor-pointer items-center gap-2.5',
                    'rounded-sm px-2.5',
                    isOtherRow
                      ? 'mt-1 border-t border-[hsl(var(--border-light))] pt-3 pb-1.5 font-mono'
                      : 'py-1.5 font-body',
                    'text-[13px] text-foreground',
                    isAct && 'bg-[hsl(var(--bg-hover))]',
                    isSel ? 'font-bold' : 'font-medium'
                  )}
                >
                  <span aria-hidden="true" className="text-[15px] leading-none">
                    {g.emoji}
                  </span>
                  <span className="flex-1">{g.label}</span>
                  {isSel && (
                    <span aria-hidden="true" className="font-extrabold text-[hsl(var(--c-game))]">
                      ✓
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {isOther && (
        <input
          ref={otherInputRef}
          type="text"
          value={otherText}
          onChange={e => onOtherText(e.target.value)}
          placeholder={labels.otherPlaceholder}
          aria-label={labels.otherInputAriaLabel}
          disabled={disabled}
          className={clsx(
            'mt-2 block w-full px-3 py-2',
            'rounded-md border-[1.5px] border-border bg-background',
            'font-body text-[13px] text-foreground',
            'outline-none',
            'focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
            disabled && 'cursor-not-allowed opacity-65'
          )}
        />
      )}

      {error && (
        <div
          id={errorId}
          role="alert"
          className="mt-1 text-[11px] font-semibold text-[hsl(var(--c-danger))]"
        >
          {error}
        </div>
      )}
    </div>
  );
}
