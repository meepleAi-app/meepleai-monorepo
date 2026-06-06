'use client';

/**
 * GameNightEventDrawerContent — cascade drawer for GameNight entities.
 *
 * Issue #1929 Task C Macro 2 — WP3.
 *
 * Renders a two-tab drawer (Overview + Giocatori) for a GameNight entity.
 * The Giocatori tab lists RSVPs with click-to-pushDrawer so the player
 * drawer cascades on top (Journey #1 cross-asse flow).
 *
 * Data:
 *   - useGameNight(entityId)    → GameNightDto
 *   - useGameNightRsvps(entityId) → GameNightRsvpDto[]
 *
 * Design: reuses rose/event palette (350 89% 60%) matching ENTITY_COLORS.gameNightEvent.
 */

import React, { useState } from 'react';

import { CalendarDays, ExternalLink, MapPin, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { useGameNight, useGameNightRsvps } from '@/hooks/queries/useGameNights';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

import { DrawerLoadingSkeleton, DrawerErrorState } from '../drawer-states';
import { DrawerActionFooter } from '../DrawerActionFooter';
import { ENTITY_COLORS, EntityHeader, EntityTabTrigger, StatCard } from '../shared';

import type { DrawerAction } from '../DrawerActionFooter';

// ============================================================================
// Types
// ============================================================================

interface GameNightEventDrawerContentProps {
  entityId: string;
}

type GameNightTab = 'overview' | 'giocatori';

// ============================================================================
// Component
// ============================================================================

export function GameNightEventDrawerContent({ entityId }: GameNightEventDrawerContentProps) {
  const { data: gameNight, isLoading: gnLoading, isError: gnError } = useGameNight(entityId);
  const { data: rsvps, isLoading: rsvpsLoading } = useGameNightRsvps(entityId);

  const [activeTab, setActiveTab] = useState<GameNightTab>('overview');
  const router = useRouter();
  const colors = ENTITY_COLORS.gameNightEvent;

  const pushDrawer = useCascadeNavigationStore(s => s.pushDrawer);

  const loading = gnLoading || rsvpsLoading;

  if (loading) return <DrawerLoadingSkeleton />;
  if (gnError || !gameNight) return <DrawerErrorState error="Game Night non trovato" />;

  const rsvpList = rsvps ?? [];
  const acceptedCount = rsvpList.filter(r => r.status === 'Accepted').length;

  const footerActions: DrawerAction[] = [
    {
      icon: ExternalLink,
      label: 'Apri',
      onClick: () => router.push(`/game-nights/${entityId}`),
      variant: 'primary' as const,
      enabled: true,
    },
  ];

  const dateDisplay = new Date(gameNight.scheduledAt).toLocaleDateString('it-IT', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric',
  });

  const timeDisplay = new Date(gameNight.scheduledAt).toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div className="flex flex-1 flex-col">
      <EntityHeader
        title={gameNight.title}
        color={colors.hsl}
        badge={acceptedCount.toString()}
        badgeIcon={<Users className="h-3 w-3" />}
      />

      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as GameNightTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-muted/80 rounded-lg p-1">
          <EntityTabTrigger
            value="overview"
            icon={CalendarDays}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="giocatori"
            icon={Users}
            label="Giocatori"
            activeAccent={colors.activeAccent}
            badge={rsvpList.length}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* Overview tab */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Date + time */}
              <div className="rounded-lg border border-rose-200/40 bg-rose-50/50 p-3">
                <p className="font-nunito text-[10px] uppercase tracking-wider text-rose-500">
                  Data e ora
                </p>
                <p className="font-quicksand text-sm font-bold capitalize text-rose-700">
                  {dateDisplay} · {timeDisplay}
                </p>
              </div>

              {/* Location */}
              {gameNight.location && (
                <div className="flex items-center gap-2 rounded-lg border border-border/40 bg-card/50 p-3">
                  <MapPin className="h-4 w-4 shrink-0 text-rose-500" aria-hidden="true" />
                  <span className="font-nunito text-sm text-foreground">{gameNight.location}</span>
                </div>
              )}

              {/* Partecipanti stat */}
              <StatCard
                label="Confermati"
                value={`${acceptedCount}${gameNight.maxPlayers ? `/${gameNight.maxPlayers}` : ''}`}
                icon={Users}
                variant="gameNightEvent"
              />

              {/* Status */}
              <div className="flex items-center gap-2 rounded-lg border border-border/40 bg-card/50 p-3">
                <p className="font-nunito text-[10px] uppercase tracking-wider text-muted-foreground">
                  Stato
                </p>
                <span className="ml-auto font-quicksand text-xs font-bold text-foreground">
                  {gameNight.status}
                </span>
              </div>

              {/* Description */}
              {gameNight.description && (
                <div className="rounded-lg border border-border/40 bg-card/50 p-3">
                  <p className="mb-1 font-nunito text-[10px] uppercase tracking-wider text-muted-foreground">
                    Descrizione
                  </p>
                  <p className="font-nunito text-sm leading-relaxed text-foreground">
                    {gameNight.description}
                  </p>
                </div>
              )}
            </div>
          </TabsContent>

          {/* Giocatori tab */}
          <TabsContent value="giocatori" className="mt-0">
            <div className="space-y-2">
              {rsvpList.length === 0 ? (
                <p className="py-8 text-center font-nunito text-xs text-muted-foreground">
                  Nessun giocatore invitato
                </p>
              ) : (
                rsvpList.map(rsvp => (
                  <button
                    key={rsvp.id}
                    type="button"
                    onClick={() => pushDrawer('player', rsvp.userId)}
                    data-testid={`gamenight-player-${rsvp.userId}`}
                    className="flex w-full items-center justify-between gap-2 rounded-lg border border-border/40 bg-card/50 p-2.5 text-left transition-colors hover:border-border-strong hover:bg-card"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-nunito text-sm font-medium text-foreground">
                        {rsvp.userName}
                      </p>
                    </div>
                    <RsvpStatusBadge status={rsvp.status} />
                  </button>
                ))
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>

      <DrawerActionFooter actions={footerActions} />
    </div>
  );
}

// ============================================================================
// RsvpStatusBadge — inline since it's drawer-specific
// ============================================================================

type RsvpStatus = 'Pending' | 'Accepted' | 'Declined' | 'Maybe';

const RSVP_LABELS: Record<RsvpStatus, string> = {
  Pending: 'In attesa',
  Accepted: 'Confermato',
  Declined: 'Rifiutato',
  Maybe: 'Forse',
};

const RSVP_COLORS: Record<RsvpStatus, string> = {
  Pending: 'bg-amber-100 text-amber-700',
  Accepted: 'bg-green-100 text-green-700',
  Declined: 'bg-red-100 text-red-700',
  Maybe: 'bg-blue-100 text-blue-700',
};

function RsvpStatusBadge({ status }: { status: RsvpStatus }) {
  return (
    <span
      className={`shrink-0 rounded-full px-2 py-0.5 font-nunito text-[10px] font-bold ${RSVP_COLORS[status]}`}
    >
      {RSVP_LABELS[status]}
    </span>
  );
}
