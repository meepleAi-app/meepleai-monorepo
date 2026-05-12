'use client';

import { useEffect } from 'react';

interface TopBarSearchPillProps {
  /** Called when the pill is clicked or ⌘K/Ctrl+K is pressed */
  onOpen?: () => void;
  placeholder?: string;
}

export function TopBarSearchPill({
  onOpen,
  placeholder = 'Cerca giochi, sessioni, regole, giocatori…',
}: TopBarSearchPillProps) {
  useEffect(() => {
    if (!onOpen) return;

    const handler = (e: KeyboardEvent) => {
      if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        onOpen();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onOpen]);

  return (
    <div className="flex-1 min-w-0 px-2 max-w-[420px]">
      <button
        type="button"
        aria-label="Search"
        onClick={() => onOpen?.()}
        className="w-full flex items-center gap-3 px-5 py-[11px] rounded-xl border border-[var(--glass-border)] bg-[var(--bg-card)] text-left text-[0.82rem] text-[var(--text-muted)] hover:bg-[var(--glass-bg)] hover:border-[var(--border-glass-hover)] hover:shadow-[var(--shadow-warm-sm)] transition-all"
      >
        <span className="shrink-0 text-sm" aria-hidden>
          🔍
        </span>
        <span className="flex-1 truncate">{placeholder}</span>
        <span
          className="shrink-0 px-2 py-0.5 rounded-[5px] border border-[var(--glass-border)] bg-[var(--glass-bg)] text-[10px] font-mono font-bold text-[var(--text-sec)]"
          aria-hidden
        >
          ⌘K
        </span>
      </button>
    </div>
  );
}
