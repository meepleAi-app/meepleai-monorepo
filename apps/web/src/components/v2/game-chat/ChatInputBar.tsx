/**
 * ChatInputBar — input controlled + send button.
 * Submit on Enter o click su send. Trim value, blocca submit se whitespace-only.
 * Spec: §3.3
 */
'use client';

import type { ChangeEvent, KeyboardEvent, ReactElement } from 'react';
import clsx from 'clsx';

export interface ChatInputBarProps {
  readonly value: string;
  readonly onChange: (next: string) => void;
  readonly onSubmit: (question: string) => void;
  readonly disabled?: boolean;
  readonly placeholder?: string;
  readonly className?: string;
}

export function ChatInputBar({
  value,
  onChange,
  onSubmit,
  disabled = false,
  placeholder = 'Scrivi una domanda…',
  className,
}: ChatInputBarProps): ReactElement {
  const trySubmit = () => {
    const trimmed = value.trim();
    if (trimmed.length === 0) return;
    onSubmit(trimmed);
  };

  const handleKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      trySubmit();
    }
  };

  return (
    <div
      data-slot="chat-input-bar"
      className={clsx(
        'flex items-center gap-2 border-t border-border-light px-4 py-3',
        className
      )}
    >
      <input
        type="text"
        aria-label="Messaggio"
        value={value}
        onChange={(e: ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
        onKeyDown={handleKey}
        disabled={disabled}
        placeholder={placeholder}
        className={clsx(
          'flex-1 rounded-full border border-border bg-muted px-4 py-3',
          'text-base focus:outline-none focus:border-[hsl(var(--c-chat)/0.5)]',
          'focus:shadow-[0_0_0_3px_hsl(var(--c-chat)/0.1)]',
          'disabled:opacity-50'
        )}
      />
      <button
        type="button"
        aria-label="Invia"
        onClick={trySubmit}
        disabled={disabled}
        className={clsx(
          'flex h-11 w-11 items-center justify-center rounded-full',
          'bg-[hsl(var(--c-chat))] text-white text-lg',
          'disabled:opacity-50'
        )}
      >
        ↑
      </button>
    </div>
  );
}
