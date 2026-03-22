'use client';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

interface ActiveSessionCardProps {
  session: {
    id: string;
    gameName: string;
    gameImageUrl?: string;
    playerCount: number;
    duration: string;
    gameId?: string;
  };
}

export function ActiveSessionCard({ session }: ActiveSessionCardProps) {
  return (
    <div className="rounded-xl border-l-4 border-l-emerald-500 bg-[#21262d]">
      <MeepleCard
        entity="session"
        variant="list"
        title={session.gameName}
        subtitle={`Sessione in corso • ${session.playerCount} giocatori • ${session.duration}`}
        imageUrl={session.gameImageUrl}
        badge="Attiva"
      />
    </div>
  );
}
