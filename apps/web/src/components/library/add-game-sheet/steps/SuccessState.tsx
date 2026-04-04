'use client';

/**
 * SuccessState - Post-save success view with navigation links.
 * Issue #4821: Step 3 Info & Save
 * Epic #4817: User Collection Wizard
 *
 * Task 23 (Game Night Improvvisata): Added PdfProcessingBanner with
 * 4 animated stages shown when a PDF rulebook was uploaded.
 */

import { useEffect, useRef, useState } from 'react';

import { CheckCircle2, Library, ExternalLink, PlusCircle, Loader2 } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

// ─── PDF Processing Banner ────────────────────────────────────────────────────

const PDF_STAGES = [
  { id: 'chunking', label: 'Suddivisione in sezioni' },
  { id: 'embedding', label: 'Generazione embedding' },
  { id: 'indexing', label: 'Indicizzazione' },
  { id: 'ready', label: 'Pronto' },
] as const;

const STAGE_DURATION_MS = 3000;

function PdfProcessingBanner() {
  const [stageIndex, setStageIndex] = useState(0);
  const isDone = stageIndex >= PDF_STAGES.length - 1;

  useEffect(() => {
    if (isDone) return;
    const id = setInterval(() => {
      setStageIndex(prev => {
        if (prev >= PDF_STAGES.length - 1) {
          clearInterval(id);
          return prev;
        }
        return prev + 1;
      });
    }, STAGE_DURATION_MS);
    return () => clearInterval(id);
  }, [isDone]);

  const progress = ((stageIndex + 1) / PDF_STAGES.length) * 100;

  return (
    <div
      className="mt-6 w-full max-w-xs rounded-xl border border-amber-500/20 bg-amber-500/10 p-4 text-left"
      role="status"
      aria-label="Stato elaborazione PDF"
      data-testid="pdf-processing-banner"
    >
      <div className="flex items-center gap-2 mb-3">
        {isDone ? (
          <CheckCircle2 className="h-4 w-4 text-teal-400 shrink-0" />
        ) : (
          <Loader2 className="h-4 w-4 text-amber-400 animate-spin shrink-0" />
        )}
        <p className="text-xs font-medium text-amber-200">
          {isDone ? 'Regolamento indicizzato!' : 'Elaborazione regolamento PDF...'}
        </p>
      </div>

      {/* Stage list */}
      <ol className="space-y-1 mb-3" aria-label="Fasi di elaborazione">
        {PDF_STAGES.map((stage, i) => (
          <li
            key={stage.id}
            className={`flex items-center gap-2 text-xs ${
              i < stageIndex
                ? 'text-teal-400'
                : i === stageIndex
                  ? 'text-amber-200 font-medium'
                  : 'text-slate-500'
            }`}
          >
            {i < stageIndex ? (
              <CheckCircle2 className="h-3 w-3 shrink-0" />
            ) : i === stageIndex && !isDone ? (
              <Loader2 className="h-3 w-3 animate-spin shrink-0" />
            ) : i === stageIndex && isDone ? (
              <CheckCircle2 className="h-3 w-3 text-teal-400 shrink-0" />
            ) : (
              <span className="h-3 w-3 shrink-0 rounded-full border border-slate-600 inline-block" />
            )}
            {stage.label}
          </li>
        ))}
      </ol>

      {/* Progress bar */}
      <div
        className="w-full h-1 rounded-full bg-amber-900/40 overflow-hidden"
        role="progressbar"
        aria-valuenow={Math.round(progress)}
        aria-valuemin={0}
        aria-valuemax={100}
      >
        <div
          className="h-full rounded-full bg-amber-400 transition-all duration-700 ease-in-out"
          style={{ width: `${progress}%` }}
        />
      </div>
    </div>
  );
}

// ─── SuccessState ─────────────────────────────────────────────────────────────

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
  /**
   * Whether a PDF rulebook was uploaded. When true, shows the
   * PdfProcessingBanner with animated pipeline stages (Task 23).
   */
  hasPdf?: boolean;
}

export function SuccessState({
  gameTitle,
  libraryEntryId: _libraryEntryId,
  gameId,
  onAddAnother,
  onAutoClose,
  autoCloseDelay = 5000,
  hasPdf = false,
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

      {/* PDF processing indicator */}
      {hasPdf && <PdfProcessingBanner />}

      {/* Navigation links */}
      <div className={`flex flex-col gap-2.5 w-full max-w-xs ${hasPdf ? 'mt-6' : ''}`}>
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
