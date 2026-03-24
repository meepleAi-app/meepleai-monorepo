'use client';

import { ArrowRight } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { useNextSession } from '@/hooks/queries/useNextSession';
import { cn } from '@/lib/utils';

interface LibraryHeroBannerProps {
  hide?: boolean;
  className?: string;
}

export function LibraryHeroBanner({ hide, className }: LibraryHeroBannerProps) {
  const { data: session, isLoading } = useNextSession();

  if (hide) return null;

  if (isLoading) {
    return <div className={cn('h-[72px] animate-pulse rounded-[14px] bg-muted', className)} />;
  }

  // Session variant — shown when an upcoming session exists
  if (session) {
    const gamesText = session.games.length > 0 ? session.games.join(', ') : '';
    return (
      <div
        role="banner"
        aria-label="Prossima sessione"
        className={cn(
          'flex items-center justify-between rounded-[14px] border border-primary/20 px-5 py-4',
          'bg-gradient-to-r from-primary/15 via-accent/8 to-blue-500/8',
          'flex-col gap-2 sm:flex-row sm:gap-4',
          className
        )}
      >
        <div className="flex flex-col gap-1">
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-primary/20 px-2.5 py-0.5 font-quicksand text-[10px] font-bold uppercase tracking-wider text-primary">
            <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-primary" />
            Prossima sessione
          </span>
          <span className="font-quicksand text-base font-bold sm:text-lg">{session.name}</span>
          <span className="text-xs text-muted-foreground sm:text-sm">
            {session.playerCount} giocatori{gamesText ? ` · ${gamesText}` : ''}
          </span>
        </div>
        <Button asChild size="sm" className="shrink-0">
          <Link href={`/sessions/${session.id}`}>
            Vedi Dettagli <ArrowRight className="ml-1 h-3.5 w-3.5" />
          </Link>
        </Button>
      </div>
    );
  }

  // Discovery variant — default when no upcoming session
  return (
    <div
      role="banner"
      aria-label="Esplora il catalogo"
      className={cn(
        'flex items-center justify-between rounded-[14px] border border-primary/20 px-5 py-4',
        'bg-gradient-to-r from-primary/15 via-accent/8 to-blue-500/8',
        'flex-col gap-2 sm:flex-row sm:gap-4',
        className
      )}
    >
      <div className="flex flex-col gap-1">
        <span className="font-quicksand text-base font-bold sm:text-lg">Scopri nuovi giochi</span>
        <span className="text-xs text-muted-foreground sm:text-sm">
          Esplora il catalogo e arricchisci la tua collezione
        </span>
      </div>
      <Button asChild size="sm" variant="default" className="shrink-0">
        <Link href="/games">
          Esplora Catalogo <ArrowRight className="ml-1 h-3.5 w-3.5" />
        </Link>
      </Button>
    </div>
  );
}
