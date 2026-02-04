/**
 * Error Boundary for Library Game Detail Page (Issue #3513)
 *
 * Displays user-friendly error message with recovery options.
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

export default function LibraryGameDetailError({ error, reset }: ErrorProps) {
  useEffect(() => {
    // Log error to monitoring service
    console.error('Library Game Detail Error:', error);
  }, [error]);

  return (
    <div className="min-h-screen bg-[#FAF8F5]">
      {/* Background gradient decoration */}
      <div
        className="pointer-events-none fixed inset-0"
        style={{
          background: `
            radial-gradient(ellipse at 20% 30%, hsla(25, 40%, 90%, 0.4) 0%, transparent 50%),
            radial-gradient(ellipse at 80% 70%, hsla(262, 30%, 92%, 0.3) 0%, transparent 50%)
          `,
        }}
      />

      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-[rgba(45,42,38,0.08)] bg-[rgba(250,248,245,0.85)] backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
          <Link
            href="/library"
            className="group flex items-center gap-2 font-quicksand text-sm font-semibold text-[#6B665C] transition-colors hover:text-[hsl(25,95%,38%)]"
          >
            <ArrowLeft className="h-5 w-5 transition-transform group-hover:-translate-x-1" />
            <span>Torna alla Collezione</span>
          </Link>
          <span className="font-quicksand text-lg font-bold tracking-tight text-[hsl(25,95%,38%)]">
            MeepleAI
          </span>
        </div>
      </header>

      {/* Error Content */}
      <main className="relative z-10 mx-auto flex max-w-6xl flex-col items-center justify-center px-4 py-16 sm:px-6">
        <div
          className="w-full max-w-md overflow-hidden rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] p-8 text-center"
          style={{ boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)' }}
        >
          {/* Icon */}
          <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
            <AlertTriangle className="h-8 w-8 text-[hsl(25,95%,38%)]" />
          </div>

          {/* Title */}
          <h1 className="mb-2 font-quicksand text-2xl font-bold text-[#2D2A26]">
            Ops! Qualcosa è andato storto
          </h1>

          {/* Description */}
          <p className="mb-6 font-nunito text-[#6B665C]">
            Si è verificato un errore nel caricamento della pagina del gioco.
            Prova a ricaricare o torna alla libreria.
          </p>

          {/* Error details (development only) */}
          {process.env.NODE_ENV === 'development' && (
            <div className="mb-6 rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-left">
              <p className="mb-1 font-quicksand text-xs font-semibold uppercase tracking-wider text-[#9C958A]">
                Dettagli errore
              </p>
              <p className="font-mono text-sm text-[#6B665C]">
                {error.message}
              </p>
              {error.digest && (
                <p className="mt-1 font-mono text-xs text-[#9C958A]">
                  Digest: {error.digest}
                </p>
              )}
            </div>
          )}

          {/* Actions */}
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
                Torna alla Libreria
              </Link>
            </Button>
          </div>
        </div>
      </main>
    </div>
  );
}
