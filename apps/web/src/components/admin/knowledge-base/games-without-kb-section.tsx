'use client';

import { useState } from 'react';

import { AlertCircle, CheckCircle2, Upload } from 'lucide-react';

import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/primitives/input';
import { useGamesWithoutKb, type GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';

interface GamesWithoutKbSectionProps {
  onUploadClick: (game: GameWithoutKbDto) => void;
}

/**
 * Admin RAG onboarding — grid of SharedGames with no Knowledge Base yet.
 * Each card exposes an "Aggiungi PDF" CTA that opens the upload drawer.
 *
 * See: docs/superpowers/plans/2026-04-11-rag-admin-onboarding.md (Task 5)
 */
export function GamesWithoutKbSection({ onUploadClick }: GamesWithoutKbSectionProps) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useGamesWithoutKb({
    page,
    pageSize: 20,
    search: search || undefined,
  });

  if (isLoading) {
    return (
      <div
        data-testid="games-without-kb-loading"
        className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5"
      >
        {Array.from({ length: 10 }).map((_, i) => (
          <MeepleCardSkeleton key={i} variant="grid" />
        ))}
      </div>
    );
  }

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="space-y-4">
      <Input
        type="search"
        placeholder="Cerca gioco senza KB..."
        value={search}
        onChange={e => {
          setSearch(e.target.value);
          setPage(1);
        }}
        className="max-w-sm"
        aria-label="Cerca gioco senza Knowledge Base"
      />

      {items.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-16 text-slate-400 dark:text-zinc-500">
          <CheckCircle2 className="h-10 w-10 text-emerald-400" />
          <p className="text-sm">Tutti i giochi hanno una KB attiva</p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
            {items.map(game => (
              <div key={game.gameId} className="flex flex-col gap-2">
                <MeepleCard
                  entity="game"
                  variant="grid"
                  title={game.title}
                  subtitle={game.publisher ?? undefined}
                  imageUrl={game.imageUrl ?? undefined}
                  status={game.failedPdfCount > 0 ? 'failed' : 'idle'}
                  badge={
                    game.failedPdfCount > 0
                      ? `${game.failedPdfCount} ${game.failedPdfCount === 1 ? 'fallito' : 'falliti'}`
                      : 'Nessuna KB'
                  }
                  metadata={[
                    { label: 'Giocatori', value: game.playerCountLabel },
                    ...(game.pdfCount > 0
                      ? [{ label: 'PDF caricati', value: String(game.pdfCount) }]
                      : []),
                  ]}
                />
                <button
                  type="button"
                  onClick={() => onUploadClick(game)}
                  className="flex items-center justify-center gap-2 rounded-md bg-orange-600 px-3 py-2 text-xs font-semibold text-white shadow-sm transition-colors hover:bg-orange-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-orange-600"
                  aria-label={`Aggiungi PDF per ${game.title}`}
                >
                  {game.failedPdfCount > 0 ? (
                    <AlertCircle className="h-3.5 w-3.5" aria-hidden="true" />
                  ) : (
                    <Upload className="h-3.5 w-3.5" aria-hidden="true" />
                  )}
                  Aggiungi PDF
                </button>
              </div>
            ))}
          </div>

          {totalPages > 1 && (
            <nav
              aria-label="Paginazione giochi senza KB"
              className="flex justify-center gap-2 pt-2"
            >
              <button
                type="button"
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                className="rounded border border-slate-200 px-3 py-1 text-sm disabled:opacity-40 dark:border-zinc-700"
                aria-label="Pagina precedente"
              >
                ‹
              </button>
              <span
                className="px-3 py-1 text-sm text-slate-500 dark:text-zinc-400"
                aria-live="polite"
              >
                {page} / {totalPages}
              </span>
              <button
                type="button"
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="rounded border border-slate-200 px-3 py-1 text-sm disabled:opacity-40 dark:border-zinc-700"
                aria-label="Pagina successiva"
              >
                ›
              </button>
            </nav>
          )}
        </>
      )}
    </div>
  );
}
