'use client';

/**
 * SuccessState - Post-save success view with navigation links.
 * Issue #4821: Step 3 Info & Save
 * Epic #4817: User Collection Wizard
 */

import { useEffect, useRef } from 'react';

import { CheckCircle2, Library, ExternalLink, PlusCircle } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/button';

export interface SuccessStateProps {
  /** The saved game title */
  gameTitle: string;
  /** Library entry ID for navigation */
  libraryEntryId?: string;
  /** Game ID for navigation */
  gameId?: string;
  /** Called when "Add another game" is clicked */
  onAddAnother: () => void;
  /** Called to auto-close the drawer */
  onAutoClose: () => void;
  /** Auto-close delay in ms (default: 5000) */
  autoCloseDelay?: number;
}

export function SuccessState({
  gameTitle,
  libraryEntryId: _libraryEntryId,
  gameId,
  onAddAnother,
  onAutoClose,
  autoCloseDelay = 5000,
}: SuccessStateProps) {
  const timerRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  // Auto-close after delay
  useEffect(() => {
    timerRef.current = setTimeout(onAutoClose, autoCloseDelay);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [onAutoClose, autoCloseDelay]);

  return (
    <div className="flex flex-col items-center py-10 text-center" data-testid="success-state">
      {/* Success icon */}
      <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-teal-500/10 mb-5">
        <CheckCircle2 className="h-9 w-9 text-teal-400" />
      </div>

      {/* Message */}
      <h3 className="text-lg font-semibold text-slate-100 mb-1.5">
        Gioco aggiunto alla tua collezione!
      </h3>
      <p className="text-sm text-slate-400 max-w-xs mb-8">
        <span className="font-medium text-slate-300">{gameTitle}</span> è ora nella tua libreria.
      </p>

      {/* Navigation links */}
      <div className="flex flex-col gap-2.5 w-full max-w-xs">
        <Button variant="default" className="w-full gap-2 bg-teal-600 hover:bg-teal-700" asChild>
          <Link href="/library">
            <Library className="h-4 w-4" />
            Vai alla collezione
          </Link>
        </Button>

        {gameId && (
          <Button variant="outline" className="w-full gap-2" asChild>
            <Link href={`/library/games/${gameId}`}>
              <ExternalLink className="h-4 w-4" />
              Vedi dettaglio gioco
            </Link>
          </Button>
        )}

        <Button
          variant="ghost"
          className="w-full gap-2 text-slate-400 hover:text-slate-200"
          onClick={onAddAnother}
          data-testid="add-another-button"
        >
          <PlusCircle className="h-4 w-4" />
          Aggiungi un altro gioco
        </Button>
      </div>
    </div>
  );
}
