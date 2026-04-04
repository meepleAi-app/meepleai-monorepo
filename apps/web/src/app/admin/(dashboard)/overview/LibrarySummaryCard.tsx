'use client';

import { Library } from 'lucide-react';
import Link from 'next/link';

// ============================================================================
// Types
// ============================================================================

export interface RecentGame {
  id: string;
  title: string;
  createdAt: string;
}

export interface LibrarySummaryCardProps {
  totalGames: number;
  recentGames: RecentGame[];
}

// ============================================================================
// Helpers
// ============================================================================

function formatItalianDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

// ============================================================================
// Component
// ============================================================================

export function LibrarySummaryCard({ totalGames, recentGames }: LibrarySummaryCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5 flex flex-col gap-4">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-orange-100/80 dark:bg-orange-500/20">
          <Library className="h-5 w-5 text-orange-700 dark:text-orange-400" />
        </div>
        <span className="font-quicksand font-semibold text-foreground">Libreria Condivisa</span>
      </div>

      {/* Total games stat */}
      <div>
        <p className="text-2xl font-bold text-foreground" data-testid="library-total-games">
          {totalGames.toLocaleString()}
        </p>
        <p className="text-sm text-muted-foreground">giochi nel catalogo</p>
      </div>

      {/* Recent games section */}
      <div className="flex flex-col gap-2">
        <p className="text-xs uppercase tracking-wide text-muted-foreground font-semibold">
          Ultimi aggiunti
        </p>

        {recentGames.length === 0 ? (
          <p className="text-sm italic text-muted-foreground">Nessun gioco recente</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {recentGames.map(game => (
              <li key={game.id}>
                <Link
                  href={`/admin/shared-games/${game.id}`}
                  className="flex items-center justify-between text-sm hover:underline text-foreground"
                >
                  <span>{game.title}</span>
                  <span className="text-xs text-muted-foreground">
                    {formatItalianDate(game.createdAt)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Footer link */}
      <div className="mt-auto pt-2 border-t border-slate-200/60 dark:border-zinc-700/40">
        <Link
          href="/admin/shared-games/all"
          className="text-sm text-orange-600 dark:text-orange-400 hover:underline font-medium"
        >
          Gestisci catalogo →
        </Link>
      </div>
    </div>
  );
}

export default LibrarySummaryCard;
