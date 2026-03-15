'use client';

import type { HeroPriority } from '@/hooks/useDashboardContext';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

import { GameNightHero } from './game-night-hero';
import { IncompleteSessionHero } from './incomplete-session-hero';
import { DashboardSessionHero } from './session-hero';

interface HeroZoneProps {
  hero: HeroPriority;
  lastSession?: SessionSummaryDto;
}

export function HeroZone({ hero, lastSession }: HeroZoneProps) {
  switch (hero.type) {
    case 'active-session':
      return <DashboardSessionHero lastSession={lastSession} />;
    case 'upcoming-game-night':
      return (
        <GameNightHero
          gameNight={hero.data as { id: string; title: string; scheduledAt: string }}
        />
      );
    case 'incomplete-session':
      return (
        <IncompleteSessionHero
          session={hero.data as { id: string; gameName: string; sessionDate: string }}
        />
      );
    case 'last-played':
      return <DashboardSessionHero lastSession={lastSession} />;
    case 'welcome':
      if (lastSession) return <DashboardSessionHero lastSession={lastSession} />;
      return null;
  }
}
