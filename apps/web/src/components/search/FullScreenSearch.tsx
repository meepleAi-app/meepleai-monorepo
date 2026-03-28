'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';

import { AnimatePresence, motion } from 'framer-motion';
import { Search, X } from 'lucide-react';

import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';

/**
 * Re-export BggSearchResult as BggGameResult for consumers
 * that prefer the game-specific naming.
 */
export type BggGameResult = BggSearchResult;

export interface FullScreenSearchProps {
  open: boolean;
  onClose: () => void;
  onSelectGame: (game: BggGameResult) => void;
}

export function FullScreenSearch({ open, onClose, onSelectGame }: FullScreenSearchProps) {
  const [query, setQuery] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const { data, isLoading, error } = useSearchBggGames({
    query,
    exact: false,
  });

  // Auto-focus when opened
  useEffect(() => {
    if (open) {
      // Small delay to let the animation start
      const timer = setTimeout(() => inputRef.current?.focus(), 100);
      return () => clearTimeout(timer);
    } else {
      setQuery('');
    }
  }, [open]);

  // Close on Escape
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    },
    [onClose]
  );

  useEffect(() => {
    if (open) {
      document.addEventListener('keydown', handleKeyDown);
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      if (open) {
        document.body.style.overflow = '';
      }
    };
  }, [open, handleKeyDown]);

  if (!open) return null;

  const results = data?.results ?? [];
  const hasQuery = query.trim().length >= 2;

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[60] flex flex-col bg-[var(--gaming-bg-primary,#0a0a1a)]"
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -20 }}
          transition={{ duration: 0.2 }}
        >
          {/* Header with search input */}
          <div className="flex items-center gap-3 border-b border-white/10 px-4 py-3">
            <Search className="h-5 w-5 shrink-0 text-[var(--gaming-text-secondary)]" />
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={e => setQuery(e.target.value)}
              placeholder="Cerca un gioco su BoardGameGeek..."
              className="flex-1 bg-transparent text-base text-[var(--gaming-text-primary)] placeholder:text-[var(--gaming-text-secondary)] focus:outline-none"
            />
            <button
              onClick={onClose}
              aria-label="Chiudi ricerca"
              className="rounded-full p-1.5 text-[var(--gaming-text-secondary)] transition-colors hover:bg-white/10 hover:text-[var(--gaming-text-primary)]"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          {/* Results area */}
          <div className="flex-1 overflow-y-auto">
            {/* Loading spinner */}
            {isLoading && (
              <div className="flex items-center justify-center py-12">
                <svg
                  className="h-8 w-8 animate-spin text-[var(--gaming-accent)]"
                  viewBox="0 0 24 24"
                  fill="none"
                  aria-label="Caricamento..."
                >
                  <circle
                    className="opacity-25"
                    cx="12"
                    cy="12"
                    r="10"
                    stroke="currentColor"
                    strokeWidth="4"
                  />
                  <path
                    className="opacity-75"
                    fill="currentColor"
                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                  />
                </svg>
              </div>
            )}

            {/* Error state */}
            {error && !isLoading && (
              <div className="px-4 py-12 text-center text-[var(--gaming-text-secondary)]">
                <p>Errore nella ricerca. Riprova.</p>
              </div>
            )}

            {/* Prompt to type more */}
            {!hasQuery && !isLoading && (
              <div className="px-4 py-12 text-center text-[var(--gaming-text-secondary)]">
                <p>Digita almeno 2 caratteri per cercare</p>
              </div>
            )}

            {/* No results */}
            {hasQuery && !isLoading && !error && results.length === 0 && (
              <div className="px-4 py-12 text-center text-[var(--gaming-text-secondary)]">
                <p>Nessun risultato per &quot;{query}&quot;</p>
              </div>
            )}

            {/* Results list */}
            {results.length > 0 && !isLoading && (
              <ul className="divide-y divide-white/5">
                {results.map(game => (
                  <li key={game.bggId}>
                    <button
                      onClick={() => onSelectGame(game)}
                      className="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-white/5 active:bg-white/10"
                    >
                      {game.thumbnailUrl ? (
                        <img
                          src={game.thumbnailUrl}
                          alt=""
                          className="h-12 w-12 shrink-0 rounded-md object-cover"
                        />
                      ) : (
                        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-md bg-white/10">
                          <Search className="h-5 w-5 text-[var(--gaming-text-secondary)]" />
                        </div>
                      )}
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium text-[var(--gaming-text-primary)]">
                          {game.name}
                        </p>
                        {game.yearPublished && (
                          <p className="text-xs text-[var(--gaming-text-secondary)]">
                            {game.yearPublished}
                          </p>
                        )}
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
