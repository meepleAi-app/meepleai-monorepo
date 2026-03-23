'use client';

import { useRouter } from 'next/navigation';

import { cn } from '@/lib/utils';

interface ActiveSession {
  gameName: string;
  duration: string;
  sessionId: string;
}

interface HeroZoneProps {
  userName: string;
  activeSession?: ActiveSession;
  className?: string;
}

function getTimeGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Buongiorno';
  if (hour < 18) return 'Buon pomeriggio';
  return 'Buonasera';
}

function getItalianDate(): string {
  return new Date().toLocaleDateString('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}

export function HeroZone({ userName, activeSession, className }: HeroZoneProps) {
  const router = useRouter();

  if (activeSession) {
    return (
      <div
        data-testid="hero-zone"
        className={cn(
          'rounded-2xl p-6 backdrop-blur-lg bg-gradient-to-r from-emerald-500/10 to-emerald-500/5 border border-emerald-500/20',
          className
        )}
      >
        <p className="font-quicksand font-bold text-2xl text-foreground">
          Hai una partita in corso: {activeSession.gameName}
        </p>
        <p data-testid="hero-date" className="text-muted-foreground text-sm mt-1 capitalize">
          {activeSession.duration}
        </p>
        <button
          className="mt-3 px-4 py-2 rounded-lg bg-primary text-primary-foreground text-sm font-semibold"
          onClick={() => router.push(`/sessions/${activeSession.sessionId}`)}
        >
          Riprendi
        </button>
      </div>
    );
  }

  return (
    <div
      data-testid="hero-zone"
      className={cn(
        'rounded-2xl px-7 py-6 backdrop-blur-lg bg-gradient-to-br from-[rgba(210,105,30,0.06)] to-[rgba(210,105,30,0.02)] border border-[rgba(210,105,30,0.10)]',
        className
      )}
    >
      <p className="font-quicksand font-bold text-2xl text-foreground">
        {getTimeGreeting()}, {userName} 👋
      </p>
      <p data-testid="hero-date" className="text-muted-foreground text-sm mt-1 capitalize">
        {getItalianDate()}
      </p>
    </div>
  );
}
