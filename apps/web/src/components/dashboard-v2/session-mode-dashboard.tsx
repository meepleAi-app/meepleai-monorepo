'use client';

import { useEffect, useState } from 'react';

import { useGame } from '@/hooks/queries/useGames';

import { DashboardSessionHero } from './session-hero';
import { SessionQuickActions } from './session-quick-actions';

interface SessionModeDashboardProps {
  session: { id: string; gameId: string; playerCount: number; durationMinutes: number };
}

export function SessionModeDashboard({ session }: SessionModeDashboardProps) {
  const { data: game } = useGame(session.gameId);
  // game title used implicitly via DashboardSessionHero; exposed here for future use
  const _gameTitle = game?.title ?? 'Sessione in corso';

  return (
    <div className="space-y-4">
      <DashboardSessionHero />

      <div className="flex items-center justify-between px-1">
        <SessionTimer startMinutes={session.durationMinutes} />
        <p className="font-nunito text-xs text-muted-foreground">{session.playerCount} giocatori</p>
      </div>

      <SessionQuickActions gameId={session.gameId} sessionId={session.id} />
    </div>
  );
}

function SessionTimer({ startMinutes }: { startMinutes: number }) {
  const [elapsed, setElapsed] = useState(startMinutes);

  useEffect(() => {
    const interval = setInterval(() => setElapsed(prev => prev + 1), 60_000);
    return () => clearInterval(interval);
  }, []);

  const h = Math.floor(elapsed / 60);
  const m = elapsed % 60;
  const display = h > 0 ? `${h}h ${m}m` : `${m}m`;

  return (
    <p data-testid="session-timer" className="font-quicksand text-sm font-bold text-foreground">
      {display}
    </p>
  );
}
