/**
 * DashboardClient — Stage 3 cluster orchestrator for `/dashboard` (Issue #1164).
 *
 * REFACTOR-FORWARD replacement of PR #309 (chat/session-centric, 749 LOC) per
 * spec `docs/superpowers/specs/2026-05-14-stage3-dashboard.md`.
 *
 * Composes Hero + 5 entity sections (Games / Players / Agents / Sessions /
 * Events) in a responsive grid (mobile stacked, desktop 2×2 + Events full-width
 * row). Players are derived from `useActiveSessions` (no list endpoint).
 *
 * Pattern: pure orchestrator, no business logic. Each section is a sibling
 * route-private component under `_components/sections/`.
 */

'use client';

import { useCallback, useMemo, type ReactElement } from 'react';

import { useAuth } from '@/components/auth/AuthProvider';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useAgents } from '@/hooks/queries/useAgents';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { useGames } from '@/hooks/queries/useGames';
import { useLibraryStats } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';

import { DashboardHero, type DashboardHeroKpi } from './_components/DashboardHero';
import { AgentsCompactGrid } from './_components/sections/AgentsCompactGrid';
import { EventsList, type EventListItem } from './_components/sections/EventsList';
import { GamesCarousel } from './_components/sections/GamesCarousel';
import { PlayersAvatarList, type PlayerEntry } from './_components/sections/PlayersAvatarList';
import {
  SessionsTimeline,
  type SessionTimelineItem,
} from './_components/sections/SessionsTimeline';

const SESSION_STATUS_MAP: Record<string, SessionTimelineItem['status']> = {
  InProgress: 'live',
  Live: 'live',
  Active: 'live',
  Completed: 'completed',
  Paused: 'paused',
  Setup: 'setup',
  Abandoned: 'abandoned',
};

function mapSessionStatus(raw: string): SessionTimelineItem['status'] {
  return SESSION_STATUS_MAP[raw] ?? 'completed';
}

function computeInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function colorIndexFromName(name: string): number {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0;
  return Math.abs(hash);
}

export function DashboardClient(): ReactElement {
  const { t } = useTranslation();
  const { user } = useAuth();

  // ── Data hooks ────────────────────────────────────────────────────────────
  const gamesQuery = useGames(undefined, undefined, 1, 20);
  const sessionsQuery = useActiveSessions(10);
  const agentsQuery = useAgents();
  const eventsQuery = useUpcomingGameNights();
  const statsQuery = useLibraryStats();

  // ── Derive Players from sessions (no list endpoint per AC0 Path C) ───────
  const players = useMemo<ReadonlyArray<PlayerEntry>>(() => {
    const sessions = sessionsQuery.data?.sessions ?? [];
    const seen = new Map<string, PlayerEntry>();
    for (const session of sessions) {
      for (const player of session.players ?? []) {
        const name = player.playerName;
        if (!name || seen.has(name)) continue;
        seen.set(name, {
          name,
          initials: computeInitials(name),
          colorIndex: colorIndexFromName(name),
        });
      }
    }
    return Array.from(seen.values());
  }, [sessionsQuery.data]);

  // ── Map sessions to timeline items ───────────────────────────────────────
  const sessionItems = useMemo<ReadonlyArray<SessionTimelineItem>>(() => {
    const sessions = sessionsQuery.data?.sessions ?? [];
    const games = gamesQuery.data?.games ?? [];
    const gameById = new Map(games.map(g => [g.id, g.title]));
    return sessions.map(s => ({
      id: s.id,
      title: gameById.get(s.gameId) ?? t('pages.dashboard.sections.sessions.untitled'),
      status: mapSessionStatus(s.status),
      playerCount: s.playerCount,
      durationMinutes: s.durationMinutes ?? null,
    }));
  }, [sessionsQuery.data, gamesQuery.data, t]);

  // ── Map events to list items ─────────────────────────────────────────────
  const eventItems = useMemo<ReadonlyArray<EventListItem>>(() => {
    const events = eventsQuery.data ?? [];
    return events.map(e => ({
      id: e.id,
      title: e.title,
      startsAt: e.scheduledAt,
      location: e.location,
      confirmedCount: e.acceptedCount,
      pendingCount: e.pendingCount,
    }));
  }, [eventsQuery.data]);

  // ── KPI assembly ──────────────────────────────────────────────────────────
  const kpi = useMemo<DashboardHeroKpi>(
    () => ({
      games: statsQuery.data?.totalGames ?? 0,
      sessions: sessionsQuery.data?.total ?? undefined,
      // Hours-played and win-rate not yet exposed by useLibraryStats — fallback.
      hoursPlayed: undefined,
      winRate: undefined,
    }),
    [statsQuery.data, sessionsQuery.data]
  );

  // ── Telemetry handlers ────────────────────────────────────────────────────
  const handleSectionViewAll = useCallback((sectionId: string, viewAllHref: string) => {
    trackEvent('dashboard_view_all_clicked', { section: sectionId, viewAllHref });
  }, []);

  const handleEmptyCta = useCallback((sectionId: string, ctaHref: string) => {
    trackEvent('dashboard_empty_cta_clicked', { section: sectionId, ctaHref });
  }, []);

  // ── Hero labels ───────────────────────────────────────────────────────────
  const heroLabels = {
    greetingMorning: t('pages.dashboard.hero.greetingMorning'),
    greetingAfternoon: t('pages.dashboard.hero.greetingAfternoon'),
    greetingEvening: t('pages.dashboard.hero.greetingEvening'),
    subtitle: t('pages.dashboard.hero.subtitle'),
    kpiGames: t('pages.dashboard.hero.kpiGames'),
    kpiSessions: t('pages.dashboard.hero.kpiSessions'),
    kpiHours: t('pages.dashboard.hero.kpiHours'),
    kpiWinRate: t('pages.dashboard.hero.kpiWinRate'),
  };

  return (
    <main
      data-slot="dashboard-client"
      className="mx-auto flex w-full max-w-5xl flex-col gap-4 px-4 py-4 sm:gap-5 sm:px-6 sm:py-6"
    >
      <DashboardHero
        userName={user?.displayName ?? user?.email ?? t('pages.dashboard.hero.guestName')}
        kpi={kpi}
        labels={heroLabels}
      />

      <div
        data-slot="dashboard-sections-grid"
        className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:gap-4"
      >
        <GamesCarousel
          games={gamesQuery.data?.games ?? []}
          totalCount={statsQuery.data?.totalGames ?? 0}
          labels={{
            title: t('pages.dashboard.sections.games.title'),
            viewAllLabel: t('pages.dashboard.sections.games.viewAll'),
            viewAllHref: '/library',
            emptyTitle: t('pages.dashboard.sections.games.emptyTitle'),
            emptyCta: t('pages.dashboard.sections.games.emptyCta'),
            emptyCtaHref: '/library/add',
          }}
          onViewAllClick={handleSectionViewAll}
          onEmptyCtaClick={handleEmptyCta}
        />

        <PlayersAvatarList
          players={players}
          totalCount={players.length}
          labels={{
            title: t('pages.dashboard.sections.players.title'),
            viewAllLabel: t('pages.dashboard.sections.players.viewAll'),
            viewAllHref: '/players',
            countTemplate: t('pages.dashboard.sections.players.countTemplate'),
            emptyTitle: t('pages.dashboard.sections.players.emptyTitle'),
            emptyCta: t('pages.dashboard.sections.players.emptyCta'),
            emptyCtaHref: '/players',
          }}
          onViewAllClick={handleSectionViewAll}
          onEmptyCtaClick={handleEmptyCta}
        />

        <AgentsCompactGrid
          agents={agentsQuery.data ?? []}
          labels={{
            title: t('pages.dashboard.sections.agents.title'),
            viewAllLabel: t('pages.dashboard.sections.agents.viewAll'),
            viewAllHref: '/agents',
            statusActive: t('pages.dashboard.sections.agents.statusActive'),
            statusIdle: t('pages.dashboard.sections.agents.statusIdle'),
            emptyTitle: t('pages.dashboard.sections.agents.emptyTitle'),
            emptyCta: t('pages.dashboard.sections.agents.emptyCta'),
            emptyCtaHref: '/hub/agents',
          }}
          onViewAllClick={handleSectionViewAll}
          onEmptyCtaClick={handleEmptyCta}
        />

        <SessionsTimeline
          sessions={sessionItems}
          totalCount={sessionsQuery.data?.total ?? 0}
          labels={{
            title: t('pages.dashboard.sections.sessions.title'),
            liveBadge: t('pages.dashboard.sections.sessions.liveBadge'),
            viewAllLabel: t('pages.dashboard.sections.sessions.viewAll'),
            viewAllHref: '/sessions',
            playerCountTemplate: t('pages.dashboard.sections.sessions.playerCountTemplate'),
            minutesTemplate: t('pages.dashboard.sections.sessions.minutesTemplate'),
            emptyTitle: t('pages.dashboard.sections.sessions.emptyTitle'),
            emptyCta: t('pages.dashboard.sections.sessions.emptyCta'),
            emptyCtaHref: '/sessions/new',
            statusLabels: {
              live: t('pages.dashboard.sections.sessions.statusLive'),
              completed: t('pages.dashboard.sections.sessions.statusCompleted'),
              paused: t('pages.dashboard.sections.sessions.statusPaused'),
              setup: t('pages.dashboard.sections.sessions.statusSetup'),
              abandoned: t('pages.dashboard.sections.sessions.statusAbandoned'),
            },
          }}
          onViewAllClick={handleSectionViewAll}
          onEmptyCtaClick={handleEmptyCta}
        />

        <EventsList
          events={eventItems}
          labels={{
            title: t('pages.dashboard.sections.events.title'),
            viewAllLabel: t('pages.dashboard.sections.events.viewAll'),
            viewAllHref: '/game-nights',
            participantsTemplate: t('pages.dashboard.sections.events.participantsTemplate'),
            emptyTitle: t('pages.dashboard.sections.events.emptyTitle'),
            emptyCta: t('pages.dashboard.sections.events.emptyCta'),
            emptyCtaHref: '/game-nights/new',
          }}
          onViewAllClick={handleSectionViewAll}
          onEmptyCtaClick={handleEmptyCta}
        />
      </div>
    </main>
  );
}
