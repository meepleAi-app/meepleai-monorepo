'use client';

import { formatDistanceToNow } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
import { AlertTriangle } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

interface IncompleteSessionHeroProps {
  session: { id: string; gameName: string; sessionDate: string };
}

export function IncompleteSessionHero({ session }: IncompleteSessionHeroProps) {
  const timeAgo = formatDistanceToNow(new Date(session.sessionDate), {
    addSuffix: true,
    locale: itLocale,
  });

  return (
    <div className="flex items-center gap-4 rounded-2xl px-6 py-5 border-2 border-amber-300/40 bg-amber-50/50 dark:border-amber-700/30 dark:bg-amber-950/20">
      <span className="shrink-0" aria-hidden>
        <AlertTriangle className="h-8 w-8 text-amber-500" />
      </span>
      <div className="flex-1 min-w-0">
        <p className="text-xs font-bold font-quicksand uppercase tracking-widest text-amber-700 dark:text-amber-400">
          Sessione da completare
        </p>
        <h2 className="font-quicksand text-base font-bold text-foreground mt-0.5">
          {session.gameName}
        </h2>
        <p className="text-xs text-muted-foreground font-nunito mt-0.5">{timeAgo}</p>
      </div>
      <Link href={`/sessions/${session.id}`} className="shrink-0">
        <Button
          size="sm"
          className="font-quicksand font-bold gap-1.5 bg-amber-500 hover:bg-amber-600 text-white shadow-sm"
        >
          Completa
        </Button>
      </Link>
    </div>
  );
}
