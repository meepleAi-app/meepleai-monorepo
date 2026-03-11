'use client';

import { Loader2, AlertTriangle } from 'lucide-react';
import Link from 'next/link';

interface NoPdfBannerProps {
  gameId: string;
  gameName: string;
  hasPdf: boolean;
  pdfProcessingState?: string;
}

export function NoPdfBanner({ gameId, gameName, hasPdf, pdfProcessingState }: NoPdfBannerProps) {
  if (hasPdf && (!pdfProcessingState || pdfProcessingState === 'Ready')) return null;

  if (pdfProcessingState && pdfProcessingState !== 'Ready') {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-sm">
        <Loader2 className="h-4 w-4 animate-spin text-amber-500 flex-shrink-0" />
        <span>
          Analisi in corso del regolamento di <strong>{gameName}</strong>. L&apos;agente migliorerà
          le risposte al completamento.
        </span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 rounded-lg border border-slate-500/30 bg-slate-500/10 p-3 text-sm">
      <AlertTriangle className="h-4 w-4 text-slate-400 flex-shrink-0" />
      <span>
        Risposte basate su conoscenza generale (nessun PDF caricato).{' '}
        <Link
          href={`/library/games/${gameId}`}
          className="text-amber-500 hover:underline font-medium"
          aria-label="Carica regolamento"
        >
          Carica regolamento PDF
        </Link>{' '}
        per risposte più precise con citazioni.
      </span>
    </div>
  );
}
