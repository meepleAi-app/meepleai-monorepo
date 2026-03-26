'use client';

import { formatDistanceToNow } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
import { CalendarDays } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { IS_ALPHA_MODE } from '@/lib/alpha-mode';

interface GameNightHeroProps {
  gameNight: { id: string; title: string; scheduledAt: string };
}

export function GameNightHero({ gameNight }: GameNightHeroProps) {
  if (IS_ALPHA_MODE) return null;

  const timeUntil = formatDistanceToNow(new Date(gameNight.scheduledAt), {
    addSuffix: false,
    locale: itLocale,
  });

  return (
    <div
      className="relative flex items-center gap-4 rounded-2xl px-6 py-5 overflow-hidden shadow-[0_8px_32px_rgba(100,60,180,0.15)]"
      style={{ background: 'linear-gradient(135deg, hsl(262,60%,50%), hsl(262,50%,30%))' }}
      data-testid="game-night-hero-card"
    >
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            'repeating-linear-gradient(-45deg, transparent, transparent 12px, rgba(255,255,255,.05) 12px, rgba(255,255,255,.05) 13px)',
        }}
      />
      <span className="relative z-10 shrink-0">
        <CalendarDays className="h-8 w-8 text-white/80" />
      </span>
      <div className="relative z-10 flex-1 min-w-0">
        <p className="text-[11px] font-bold font-quicksand uppercase tracking-widest text-white/80">
          Game Night tra {timeUntil}
        </p>
        <h2 className="font-quicksand text-lg font-bold text-white truncate mt-0.5">
          {gameNight.title}
        </h2>
      </div>
      <Link href={`/game-nights/${gameNight.id}`} className="relative z-10 shrink-0">
        <Button
          size="sm"
          className="font-quicksand font-bold bg-white text-[hsl(262,50%,35%)] hover:bg-white/90 shadow-md"
        >
          Vedi dettagli
        </Button>
      </Link>
    </div>
  );
}
