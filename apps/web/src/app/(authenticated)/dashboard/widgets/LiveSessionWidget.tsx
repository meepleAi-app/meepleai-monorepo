'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

interface LiveSessionWidgetProps {
  session: SessionSummaryDto | undefined;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

export function LiveSessionWidget({ session, isLoading, error, onRetry }: LiveSessionWidgetProps) {
  const router = useRouter();

  if (isLoading) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} accentColor={C.success} className="animate-pulse">
        <div className="h-full" />
      </BentoWidget>
    );
  }

  if (error) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base text-destructive">
            Errore nel caricamento
          </p>
          <p className="text-sm text-muted-foreground mt-0.5">{error}</p>
        </div>
        <button
          type="button"
          onClick={e => {
            e.stopPropagation();
            onRetry();
          }}
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold border border-border hover:bg-muted/30 transition-colors"
        >
          Riprova
        </button>
      </BentoWidget>
    );
  }

  if (!session) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="border-dashed flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base text-foreground">
            Nessuna sessione attiva
          </p>
          <p className="text-sm text-muted-foreground mt-0.5">
            Avvia una nuova partita per vederla qui
          </p>
        </div>
        <Link
          href="/sessions/new"
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold text-white transition-opacity hover:opacity-90"
          style={{ background: C.game }}
          onClick={e => e.stopPropagation()}
        >
          Nuova partita
        </Link>
      </BentoWidget>
    );
  }

  return (
    <BentoWidget
      colSpan={8}
      rowSpan={2}
      accentColor={C.success}
      accentBg="rgba(16,185,129,0.04)"
      className="flex flex-col justify-between"
      onClick={() => router.push(`/sessions/${session.id}`)}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Sessione Live</WidgetLabel>
        <span
          className="inline-flex items-center gap-1.5 text-[10px] font-bold rounded-full px-2.5 py-0.5"
          style={{
            background: 'rgba(16,185,129,0.12)',
            color: C.success,
            border: '1px solid rgba(16,185,129,0.2)',
          }}
        >
          <span
            className="w-1.5 h-1.5 rounded-full animate-pulse"
            style={{ background: C.success }}
          />
          IN CORSO
        </span>
      </div>
      <div className="flex items-center gap-3">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center text-2xl shrink-0 overflow-hidden"
          style={{ background: 'rgba(255,255,255,0.06)' }}
        >
          {session.gameImageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={session.gameImageUrl}
              alt={session.gameName}
              className="w-full h-full object-cover"
            />
          ) : (
            '🎲'
          )}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-extrabold text-base leading-tight truncate">
            {session.gameName}
          </p>
          <p className="text-[11px] text-muted-foreground mt-0.5">
            {session.playerCount} giocatori
            {session.winnerName ? ` · Vincitore: ${session.winnerName}` : ''}
          </p>
        </div>
        <span
          className="shrink-0 px-3.5 py-1.5 rounded-lg text-[11px] font-bold text-white"
          style={{ background: C.game }}
          aria-hidden="true"
        >
          Entra →
        </span>
      </div>
    </BentoWidget>
  );
}
