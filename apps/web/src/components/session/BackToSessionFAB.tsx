'use client';

import { Dice5 } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useActiveSessions } from '@/hooks/queries/useActiveSessions';

export function BackToSessionFAB() {
  const { data: activeData } = useActiveSessions(1);
  const pathname = usePathname();

  const activeSession = activeData?.sessions?.[0];
  const isOnLiveSession = pathname.includes('/sessions/live/');

  if (!activeSession || isOnLiveSession) return null;

  return (
    <Link
      href={`/sessions/live/${activeSession.id}`}
      className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-full bg-primary text-primary-foreground shadow-lg hover:shadow-xl transition-all motion-reduce:transition-none"
      aria-label="Torna alla partita in corso"
    >
      <Dice5 className="w-5 h-5" />
      <span className="text-sm font-medium hidden sm:inline">Torna alla partita</span>
    </Link>
  );
}
