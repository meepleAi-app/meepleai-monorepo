/**
 * SessionCardParent
 *
 * Game Night Improvvisata — Task 15
 *
 * Main session overview card showing game name, status badge, player count,
 * connection state, pending proposals, and quick action buttons.
 */

'use client';

import { Loader2, Pause, UserPlus } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useSignalRSession } from '@/lib/domain-hooks/useSignalrSession';
import { useLiveSessionStore, type SessionStatus } from '@/lib/stores/live-session-store';

// ─── Sub-components ───────────────────────────────────────────────────────────

interface StatusBadgeProps {
  status: SessionStatus;
}

function StatusBadge({ status }: StatusBadgeProps) {
  const map: Record<SessionStatus, { label: string; className: string }> = {
    InProgress: {
      label: 'In corso',
      className: 'bg-green-100 text-green-800 border-green-200',
    },
    Paused: {
      label: 'In pausa',
      className: 'bg-amber-100 text-amber-800 border-amber-200',
    },
    Completed: {
      label: 'Completata',
      className: 'bg-gray-100 text-gray-700 border-gray-200',
    },
  };

  const { label, className } = map[status];

  return (
    <Badge variant="outline" className={className}>
      {label}
    </Badge>
  );
}

interface InfoCardProps {
  label: string;
  value: string;
}

function InfoCard({ label, value }: InfoCardProps) {
  return (
    <div className="rounded-xl bg-white/70 backdrop-blur-md border border-white/40 p-3 text-center shadow-sm">
      <p className="text-xs text-gray-500 font-nunito mb-1">{label}</p>
      <p className="text-lg font-quicksand font-bold text-gray-900">{value}</p>
    </div>
  );
}

// ─── Main Component ────────────────────────────────────────────────────────────

interface SessionCardParentProps {
  sessionId: string;
}

export function SessionCardParent({ sessionId }: SessionCardParentProps) {
  const gameName = useLiveSessionStore(s => s.gameName);
  const status = useLiveSessionStore(s => s.status);
  const players = useLiveSessionStore(s => s.players);
  const currentTurn = useLiveSessionStore(s => s.currentTurn);
  const pendingProposals = useLiveSessionStore(s => s.pendingProposals);
  const isOffline = useLiveSessionStore(s => s.isOffline);

  const { isConnected } = useSignalRSession(sessionId);

  const onlinePlayers = players.filter(p => p.isOnline).length;

  return (
    <div className="space-y-6 p-4">
      {/* Offline banner */}
      {isOffline && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 p-3 text-red-800 text-sm font-nunito">
          <Loader2 className="h-4 w-4 animate-spin shrink-0" />
          <span>Connessione persa — tentativo di riconnessione...</span>
        </div>
      )}

      {/* Header */}
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-quicksand font-bold text-gray-900 truncate">
          {gameName || 'Sessione'}
        </h1>
        <StatusBadge status={status} />
      </div>

      {/* Info grid */}
      <div className="grid grid-cols-3 gap-3">
        <InfoCard label="Turno" value={String(currentTurn)} />
        <InfoCard
          label="Giocatori"
          value={players.length > 0 ? `${onlinePlayers}/${players.length}` : '—'}
        />
        <InfoCard label="Rete" value={isConnected ? 'Online' : 'Offline'} />
      </div>

      {/* Pending proposals alert */}
      {pendingProposals.length > 0 && (
        <Link href={`/sessions/live/${sessionId}/scores`}>
          <div className="cursor-pointer rounded-xl bg-amber-50 border border-amber-200 p-4 text-amber-900 text-sm font-nunito hover:bg-amber-100 transition-colors">
            <span className="font-semibold">{pendingProposals.length}</span>{' '}
            {pendingProposals.length === 1
              ? 'proposta di punteggio in attesa'
              : 'proposte di punteggio in attesa'}
            {' — '}
            <span className="underline underline-offset-2">Vai ai punteggi</span>
          </div>
        </Link>
      )}

      {/* Quick actions */}
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" size="sm" asChild>
          <Link href={`/sessions/live/${sessionId}/players`}>
            <UserPlus className="mr-2 h-4 w-4" />
            Invita
          </Link>
        </Button>

        <Button variant="outline" size="sm" asChild>
          <Link href={`/sessions/live/${sessionId}/save`}>
            <Pause className="mr-2 h-4 w-4" />
            Pausa
          </Link>
        </Button>

        <Button variant="destructive" size="sm" asChild>
          <Link href={`/sessions/live/${sessionId}/save`}>Salva &amp; Esci</Link>
        </Button>
      </div>
    </div>
  );
}
