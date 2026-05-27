'use client';

import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ClipboardEvent,
  type KeyboardEvent,
} from 'react';

import clsx from 'clsx';

interface Props {
  /** Called once the 6 slots are filled. Receives the assembled digit string. */
  readonly onComplete: (code: string) => void;
  /** When true, applies shake animation + danger border. Caller controls it after a BE rejection. */
  readonly error?: boolean;
  /** When true, all slots are non-interactive. Caller controls this during the BE call or after lockout. */
  readonly disabled?: boolean;
}

const SLOT_COUNT = 6;

export function OTPInput6Slot({
  onComplete,
  error = false,
  disabled = false,
}: Props): React.JSX.Element {
  const [digits, setDigits] = useState<string[]>(() => Array(SLOT_COUNT).fill(''));
  const [pendingFocus, setPendingFocus] = useState<number | null>(null);
  const refs = useRef<Array<HTMLInputElement | null>>([]);

  // Post-commit side effect: focus the scheduled slot. setState updaters must be pure;
  // DOM focus runs here so React strict mode + concurrent rendering stay correct.
  useEffect(() => {
    if (pendingFocus !== null) {
      refs.current[pendingFocus]?.focus();
      setPendingFocus(null);
    }
  }, [pendingFocus]);

  const setAt = useCallback(
    (index: number, raw: string) => {
      const digit = raw.replace(/\D/g, '').slice(-1);
      // Compute next state inline (no functional updater): user-driven onChange events
      // produce a single deterministic transition, so we don't need the updater's
      // batching guarantees. This lets us call onComplete + schedule focus from outside
      // setState, keeping the updater pure (no side effects).
      const next = [...digits];
      next[index] = digit;
      setDigits(next);
      if (digit && index < SLOT_COUNT - 1) {
        setPendingFocus(index + 1);
      }
      if (index === SLOT_COUNT - 1 && digit && next.every(Boolean)) {
        onComplete(next.join(''));
      }
    },
    [digits, onComplete]
  );

  const handleKeyDown = useCallback(
    (event: KeyboardEvent<HTMLInputElement>, index: number) => {
      if (event.key === 'Backspace' && digits[index] === '' && index > 0) {
        setPendingFocus(index - 1);
      }
    },
    [digits]
  );

  const handlePaste = useCallback(
    (event: ClipboardEvent<HTMLInputElement>) => {
      event.preventDefault();
      const raw = event.clipboardData.getData('text') ?? '';
      const trimmed = raw.replace(/\D/g, '').slice(0, SLOT_COUNT);
      if (!trimmed) return;
      const next: string[] = Array(SLOT_COUNT).fill('');
      for (let i = 0; i < trimmed.length; i++) next[i] = trimmed[i];
      setDigits(next);
      const lastIdx = Math.min(trimmed.length, SLOT_COUNT) - 1;
      if (lastIdx >= 0) setPendingFocus(lastIdx);
      if (trimmed.length === SLOT_COUNT) onComplete(trimmed);
    },
    [onComplete]
  );

  return (
    <div
      role="group"
      aria-label="6-digit verification code"
      className={clsx('flex gap-2', error && 'animate-shake')}
    >
      {digits.map((digit, i) => (
        <input
          key={i}
          ref={el => {
            refs.current[i] = el;
          }}
          value={digit}
          onChange={e => setAt(i, e.target.value)}
          onKeyDown={e => handleKeyDown(e, i)}
          onPaste={handlePaste}
          disabled={disabled}
          maxLength={1}
          inputMode="numeric"
          pattern="[0-9]*"
          aria-label={`Digit ${i + 1}`}
          className={clsx(
            'w-12 h-14 text-center font-mono text-[22px] font-bold rounded-md bg-card text-foreground transition border-[1.5px]',
            error
              ? 'border-destructive bg-destructive/5'
              : 'border-border-strong focus:border-entity-kb focus:outline-none focus:ring-[3px] focus:ring-entity-kb/15 focus:scale-[1.03]',
            disabled && 'opacity-50 cursor-not-allowed'
          )}
        />
      ))}
    </div>
  );
}
