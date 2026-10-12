'use client';

import { useEffect, useRef, useState } from 'react';

import Link from 'next/link';

import type { ChatGameContext } from '@/lib/stores/chat-panel-store';

export interface AvailableGame {
  id: string;
  name: string;
}

interface ChatContextSwitcherProps {
  gameContext: ChatGameContext | null;
  onPickGame: (gameId: string) => void;
  availableGames?: AvailableGame[];
  /** Current search text (#4187). */
  search?: string;
  /**
   * When set, the dropdown searches the user's library on the server: it opens even with no
   * games, shows a search box, and offers to add a game from the catalog.
   */
  onSearchChange?: (value: string) => void;
}

export function ChatContextSwitcher({
  gameContext,
  onPickGame,
  availableGames = [],
  search = '',
  onSearchChange,
}: ChatContextSwitcherProps) {
  const searchable = onSearchChange !== undefined;
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click
  useEffect(() => {
    if (!open) return;
    function handleClick(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  function handlePillClick() {
    if (searchable || availableGames.length > 0) {
      setOpen(prev => !prev);
    } else {
      onPickGame('');
    }
  }

  function handleSelectGame(gameId: string) {
    setOpen(false);
    onPickGame(gameId);
  }

  function renderOption(game: AvailableGame) {
    return (
      <button
        key={game.id}
        type="button"
        role="option"
        aria-selected={game.id === gameContext?.id}
        onClick={() => handleSelectGame(game.id)}
        className="w-full px-4 py-2.5 text-left font-nunito text-[0.82rem] font-semibold text-[var(--text-sec)] transition-colors hover:bg-[var(--bg-card)] hover:text-[var(--text)] aria-selected:text-[hsl(25,95%,38%)]"
      >
        {game.name}
      </button>
    );
  }

  return (
    <div
      ref={wrapperRef}
      className="relative flex flex-shrink-0 flex-wrap items-center gap-2.5 border-b border-[var(--border)] bg-[var(--bg)] px-5 py-3"
    >
      <span className="mr-0.5 text-[0.7rem] font-extrabold uppercase tracking-wider text-[var(--text-muted)]">
        Contesto
      </span>
      {gameContext ? (
        <button
          type="button"
          onClick={handlePillClick}
          aria-haspopup={searchable || availableGames.length > 0 ? 'listbox' : undefined}
          aria-expanded={searchable || availableGames.length > 0 ? open : undefined}
          className="flex items-center gap-2.5 rounded-full border border-[hsla(25,95%,45%,0.35)] bg-[hsla(25,95%,45%,0.08)] py-1.5 pl-1.5 pr-3.5 shadow-[var(--shadow-warm-sm)] transition-all hover:-translate-y-px hover:shadow-[var(--shadow-warm-md)]"
        >
          <span
            aria-hidden
            className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-[7px] text-base"
            style={{
              background: 'linear-gradient(135deg, hsl(25 75% 78%), hsl(25 80% 55%))',
            }}
          >
            🎲
          </span>
          <div className="text-left">
            <div className="font-quicksand text-[0.82rem] font-extrabold leading-none text-[var(--text)]">
              {gameContext.name}
            </div>
            <div className="mt-1 text-[0.66rem] font-semibold text-[var(--text-muted)]">
              {gameContext.year ? `${gameContext.year} · ` : ''}
              {gameContext.pdfCount} PDF · KB{' '}
              {gameContext.kbStatus === 'ready' ? 'pronta' : gameContext.kbStatus}
            </div>
          </div>
          <span aria-hidden className="text-xs text-[var(--text-muted)]">
            ▾
          </span>
        </button>
      ) : (
        <button
          type="button"
          onClick={handlePillClick}
          className="rounded-full border border-dashed border-[var(--border)] bg-[var(--bg-card)] px-4 py-2 font-nunito text-[0.78rem] font-semibold text-[var(--text-muted)] hover:bg-white hover:shadow-[var(--shadow-warm-sm)]"
        >
          Seleziona gioco
        </button>
      )}

      {/* Game picker dropdown */}
      {open && searchable && (
        <div className="absolute left-5 top-full z-10 mt-1.5 w-64 rounded-2xl border border-[var(--border)] bg-[var(--bg-card)] py-1.5 shadow-[var(--shadow-warm-lg)]">
          <div className="px-3 pb-1.5">
            <input
              type="search"
              value={search}
              onChange={e => onSearchChange(e.target.value)}
              aria-label="Cerca nella tua libreria"
              placeholder="Cerca nella tua libreria…"
              autoComplete="off"
              autoFocus
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 font-nunito text-[0.8rem] text-[var(--text)]"
            />
          </div>
          {availableGames.length > 0 ? (
            <div role="listbox" aria-label="Seleziona gioco" className="max-h-52 overflow-y-auto">
              {availableGames.map(renderOption)}
            </div>
          ) : (
            <p className="px-4 py-2 font-nunito text-[0.78rem] text-[var(--text-muted)]">
              {search.trim()
                ? `Nessun gioco della tua libreria corrisponde a «${search.trim()}».`
                : 'La tua libreria è vuota.'}
            </p>
          )}
          {/* A game outside the library is not offered here: its KB stays behind the RAG
              gate (#4155) until the user adds it and declares ownership. */}
          <Link
            href="/games"
            className="block px-4 py-2 font-nunito text-[0.78rem] font-semibold text-[hsl(25,95%,38%)] hover:underline"
          >
            Aggiungi dal catalogo
          </Link>
        </div>
      )}
      {open && !searchable && availableGames.length > 0 && (
        <div
          role="listbox"
          aria-label="Seleziona gioco"
          className="absolute left-5 top-full z-10 mt-1.5 max-h-52 w-56 overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--bg-card)] py-1.5 shadow-[var(--shadow-warm-lg)]"
        >
          {availableGames.map(renderOption)}
        </div>
      )}
    </div>
  );
}
