'use client';

import { ActiveSessionBanner } from './ActiveSessionBanner';
import { CarouselSection } from './CarouselSection';
import { HeroCompact } from './HeroCompact';
import { QuickStats } from './QuickStats';

interface ActiveSession {
  sessionId: string;
  gameName: string;
  elapsed: string;
}

interface ExplorationViewProps {
  userName?: string;
  activeSession?: ActiveSession | null;
  /** Stats shown in HeroCompact inline summary */
  gamesThisWeek?: number;
  hoursPlayed?: number;
  avgRating?: number;
  /** Stats shown in QuickStats at bottom */
  totalGames?: number;
  totalSessions?: number;
}

export function ExplorationView({
  userName = '',
  activeSession,
  gamesThisWeek = 0,
  hoursPlayed = 0,
  avgRating = 0,
  totalGames = 0,
  totalSessions = 0,
}: ExplorationViewProps) {
  return (
    <div className="flex flex-col gap-5 w-full">
      {/* Greeting + inline stats */}
      <HeroCompact
        userName={userName}
        gamesThisWeek={gamesThisWeek}
        hoursPlayed={hoursPlayed}
        avgRating={avgRating}
      />

      {/* Active session banner — rendered only when a session is live */}
      {activeSession && (
        <ActiveSessionBanner
          gameName={activeSession.gameName}
          elapsed={activeSession.elapsed}
          sessionId={activeSession.sessionId}
        />
      )}

      {/* Carousel: recent games */}
      <CarouselSection
        title="Giochi Recenti"
        icon="🎲"
        seeAllHref="/library/games"
        seeAllLabel="Vedi tutti i giochi recenti"
        accentColor="hsl(25,95%,38%)"
      >
        {/* Placeholder: consumers inject MeepleCard or QuickCardsCarousel items here */}
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
      </CarouselSection>

      {/* Carousel: suggested games */}
      <CarouselSection
        title="Suggeriti per te"
        icon="✨"
        seeAllHref="/explore"
        seeAllLabel="Vedi tutti i suggerimenti"
        accentColor="hsl(270,60%,55%)"
      >
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
      </CarouselSection>

      {/* Carousel: recent sessions */}
      <CarouselSection
        title="Sessioni Recenti"
        icon="🎯"
        seeAllHref="/sessions"
        seeAllLabel="Vedi tutte le sessioni recenti"
        accentColor="hsl(160,60%,35%)"
      >
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
        <div
          role="listitem"
          className="shrink-0 w-[140px] h-[120px] rounded-2xl bg-muted/40 animate-pulse"
        />
      </CarouselSection>

      {/* Summary stats at the bottom */}
      <QuickStats totalGames={totalGames} totalSessions={totalSessions} avgRating={avgRating} />
    </div>
  );
}
