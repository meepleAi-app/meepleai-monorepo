/**
 * Error Boundary for Play Records Section
 *
 * Catches rendering errors and displays recovery UI.
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { useEffect } from 'react';

import { AlertTriangle, ArrowLeft, RefreshCcw } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

interface ErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function PlayRecordsError({ error, reset }: ErrorProps) {
  useEffect(() => {
    console.error('Play Records Error:', error);
  }, [error]);

  return (
    <div className="container mx-auto flex flex-col items-center justify-center px-4 py-16">
      <div
        className="w-full max-w-md overflow-hidden rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] p-8 text-center"
        style={{ boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)' }}
      >
        <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
          <AlertTriangle className="h-8 w-8 text-[hsl(25,95%,38%)]" />
        </div>

        <h1 className="mb-2 font-quicksand text-2xl font-bold text-[#2D2A26]">
          Ops! Qualcosa è andato storto
        </h1>

        <p className="mb-6 font-nunito text-[#6B665C]">
          Si è verificato un errore nel caricamento delle partite. Prova a ricaricare o torna alla
          dashboard.
        </p>

        {process.env.NODE_ENV === 'development' && (
          <div className="mb-6 rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-left">
            <p className="mb-1 font-quicksand text-xs font-semibold uppercase tracking-wider text-[#9C958A]">
              Dettagli errore
            </p>
            <p className="font-mono text-sm text-[#6B665C]">{error.message}</p>
            {error.digest && (
              <p className="mt-1 font-mono text-xs text-[#9C958A]">Digest: {error.digest}</p>
            )}
          </div>
        )}

        <div className="flex flex-col gap-3 sm:flex-row sm:justify-center">
          <Button
            onClick={reset}
            className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
          >
            <RefreshCcw className="mr-2 h-4 w-4" />
            Riprova
          </Button>
          <Button
            asChild
            variant="outline"
            className="border-[rgba(45,42,38,0.12)] font-quicksand font-semibold text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]"
          >
            <Link href="/library">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Torna alla Dashboard
            </Link>
          </Button>
        </div>
      </div>
    </div>
  );
}
