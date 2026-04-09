'use client';

import { useState, type KeyboardEvent } from 'react';

interface ChatInputBarProps {
  placeholder?: string;
  onSend: (value: string) => void;
}

export function ChatInputBar({ placeholder = 'Chiedi una regola…', onSend }: ChatInputBarProps) {
  const [value, setValue] = useState('');

  const send = () => {
    const trimmed = value.trim();
    if (trimmed.length === 0) return;
    onSend(trimmed);
    setValue('');
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      send();
    }
  };

  return (
    <div className="flex-shrink-0 border-t border-[var(--nh-border-default)] bg-[rgba(255,252,248,0.8)] px-5 py-4 backdrop-blur-md">
      <div className="flex items-end gap-2.5 rounded-2xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] py-2 pl-4 pr-2 shadow-[var(--shadow-warm-sm)] transition-all focus-within:border-[hsla(220,80%,55%,0.3)] focus-within:bg-white focus-within:shadow-[0_0_0_3px_hsla(220,80%,55%,0.1),var(--shadow-warm-md)]">
        <textarea
          value={value}
          onChange={e => setValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          rows={1}
          className="min-h-6 flex-1 resize-none border-none bg-transparent py-2 font-nunito text-[0.9rem] leading-relaxed text-[var(--nh-text-primary)] outline-none placeholder:text-[var(--nh-text-muted)]"
        />
        <div className="flex items-center gap-1">
          <button
            type="button"
            aria-label="Allega PDF"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-[var(--nh-text-muted)] transition-all hover:bg-[var(--nh-bg-base)] hover:text-[var(--nh-text-primary)]"
          >
            📎
          </button>
          <button
            type="button"
            aria-label="Dettatura"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-[var(--nh-text-muted)] transition-all hover:bg-[var(--nh-bg-base)] hover:text-[var(--nh-text-primary)]"
          >
            🎤
          </button>
          <button
            type="button"
            aria-label="Invia messaggio"
            onClick={send}
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-white transition-all hover:-translate-y-px"
            style={{
              background: 'linear-gradient(135deg, hsl(220 80% 58%), hsl(220 80% 42%))',
              boxShadow: '0 2px 8px hsla(220, 80%, 55%, 0.35)',
            }}
          >
            ➤
          </button>
        </div>
      </div>
      <div className="mt-2 flex items-center justify-between px-1.5 text-[0.68rem] text-[var(--nh-text-muted)]">
        <div className="flex gap-3">
          <span>
            <kbd className="rounded border border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-1.5 py-0.5 font-mono text-[9px] font-bold">
              Enter
            </kbd>{' '}
            invia
          </span>
          <span>
            <kbd className="rounded border border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-1.5 py-0.5 font-mono text-[9px] font-bold">
              ⇧ Enter
            </kbd>{' '}
            nuova riga
          </span>
        </div>
        <span>✦ Risposte basate sulla KB caricata</span>
      </div>
    </div>
  );
}
