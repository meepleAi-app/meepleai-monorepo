'use client';

import { Dice5 } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useActiveSessions } from '@/hooks/queries/useActiveSessions';

export function BackToSessionFAB() {
  const { data: activeData } = useActiveSessions(1);
  const pathname = usePathname();

  const activeSession = activeData?.sessions?.[0];
  const isOnLiveSession = pathname.includes('/sessions/') && pathname.endsWith('/live');

  if (!activeSession || isOnLiveSession) return null;

  return (
    // #4060: this FAB is the second `position: fixed` element living in the
    // bottom band, and it was the one losing the hit test — at 1920x1080 it was
    // the only control still intercepted on every page. Same z-index 50 as the
    // cookie banner, but the banner is rendered after `{children}` in
    // providers.tsx, so it paints last and wins. Stacking is the fix rather than
    // a z-index bump: both elements stay clickable instead of one covering the
    // other. `bottom` = the FAB's own 1.5rem gap + the banner's measured height
    // (169px at 1366px of width, 281px at 390px — a constant would be wrong at
    // one end or the other), collapsing back to 1.5rem once consent is given.
    <Link
      href={`/sessions/${activeSession.id}/live`}
      className="fixed bottom-[calc(1.5rem+var(--cookie-banner-height,0px))] right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-full bg-primary text-primary-foreground shadow-lg hover:shadow-xl transition-all motion-reduce:transition-none"
      aria-label="Torna alla partita in corso"
    >
      <Dice5 className="w-5 h-5" />
      <span className="text-sm font-medium hidden sm:inline">Torna alla partita</span>
    </Link>
  );
}
