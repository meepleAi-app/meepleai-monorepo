/**
 * GameNightDetailView - v2 page-client for /game-nights/[id] (Issue #951 commit 3).
 *
 * Replaces the legacy inline JSX in `page.tsx` with a composition of v2 primitives
 * from `components/features/game-night-detail/` driven by `useGameNightDetail`.
 *
 * Branching by event status:
 *   - Draft     → preserves legacy GameNightPlanningLayout (host-only flow, out of
 *                  spec-hardening AC scope per checkpoint decision 1a).
 *   - Cancelled → v2 Hero + CancelledBanner.
 *   - Published / Completed → v2 Hero + (RsvpActionBar for guests) + roster of
 *                  RsvpRow + legacy GameNightActions/SessionsList/DiaryPanel under
 *                  the hero (decision 2a — orthogonal to RSVP flow).
 *
 * Mobile sticky CTA (mockup line 850) intentionally deferred — current inline
 * RsvpActionBar placement is functional; polish PR planned (decision 3c).
 *
 * Error→toast mapping follows the rsvp-state-machine + HTTP-status keys defined
 * under `gameNightDetail.rsvp.errors.*` in the i18n bundle.
 */

'use client';

import { useEffect, useMemo } from 'react';

import { Edit, Send, XCircle } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import {
  GameNightCancelledBanner,
  GameNightDetailHero,
  GameNightRsvpActionBar,
  GameNightRsvpRow,
} from '@/components/features/game-night-detail';
import { GameNightActions } from '@/components/game-night/GameNightActions';
import { GameNightDiaryPanel } from '@/components/game-night/GameNightDiaryPanel';
import { GameNightSessionsList } from '@/components/game-night/GameNightSessionsList';
import { GameNightPlanningLayout } from '@/components/game-night/planning/GameNightPlanningLayout';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useGameNightDetail } from '@/hooks/queries/useGameNightDetail';
import { useCancelGameNight, usePublishGameNight } from '@/hooks/queries/useGameNights';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import { useToast } from '@/hooks/useToast';
import { useTranslation } from '@/hooks/useTranslation';
import type { RsvpResponse } from '@/lib/game-nights/rsvp-state-machine';
import { useGameNightStore } from '@/stores/game-night';

export function GameNightDetailView({ id }: { id: string }): React.JSX.Element {
  const router = useRouter();
  const { t, locale } = useTranslation();
  const { toast } = useToast();

  const { data: viewer } = useCurrentUser();
  const detail = useGameNightDetail(id, viewer?.id);
  const publishMutation = usePublishGameNight();
  const cancelMutation = useCancelGameNight();

  const { event, rsvps, actor, isLoading, isError, currentResponse, pendingResponse } = detail;

  // Catalog games only needed for Draft planning layout.
  const isDraft = event?.status === 'Draft';
  const { data: catalogData } = useSharedGames(undefined, isDraft);

  const { addPlayer, addGame, reset, activeSessions } = useGameNightStore();

  // Sync accepted RSVPs and game IDs from the event into the planning store
  // (preserves legacy behaviour for Draft events).
  useEffect(() => {
    if (!isDraft || !event) return;
    if (event.gameIds.length > 0 && !catalogData) return;
    reset();
    if (rsvps) {
      rsvps
        .filter(r => r.status === 'Accepted')
        .forEach(r => addPlayer({ id: r.userId, displayName: r.userName, isGuest: false }));
    }
    if (catalogData?.items && event.gameIds.length > 0) {
      const catalogMap = new Map(catalogData.items.map(g => [g.id, g]));
      event.gameIds.forEach(gid => {
        const g = catalogMap.get(gid);
        if (g) {
          addGame({
            id: g.id,
            title: g.title,
            thumbnailUrl: g.thumbnailUrl ?? undefined,
            minPlayers: g.minPlayers,
            maxPlayers: g.maxPlayers,
          });
        }
      });
    }
  }, [isDraft, event, rsvps, catalogData, reset, addPlayer, addGame]);

  // ────────────────────────────────────────────────────────────────────
  // Localized label bundles, derived once per render.
  // ────────────────────────────────────────────────────────────────────

  const dateFormatter = useMemo(
    () =>
      new Intl.DateTimeFormat(locale, {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }),
    [locale]
  );

  function handleRsvp(response: RsvpResponse) {
    const outcome = detail.submitRsvp(response);
    if (outcome.kind === 'rejected') {
      const errorKey = outcome.status === 410 ? 'cancelledGone' : 'directConflict';
      toast({
        title: t('common.error'),
        description: t(`gameNightDetail.rsvp.errors.${errorKey}`),
        variant: 'destructive',
      });
      return;
    }
    if (outcome.kind === 'submitted') {
      const toastKey: Record<RsvpResponse, string> = {
        Accepted: 'confirmedToast',
        Maybe: 'maybeToast',
        Declined: 'declinedToast',
      };
      toast({ title: t(`gameNightDetail.rsvp.${toastKey[response]}`) });
    }
  }

  function handlePublish() {
    publishMutation.mutate(id, {
      onSuccess: () =>
        toast({
          title: t('gameNightDetail.actor.host.publish'),
        }),
      onError: () =>
        toast({
          title: t('common.error'),
          description: t('gameNightDetail.rsvp.errors.generic'),
          variant: 'destructive',
        }),
    });
  }

  function handleCancel() {
    cancelMutation.mutate(id, {
      onSuccess: () => toast({ title: t('gameNightDetail.status.cancelled') }),
      onError: () =>
        toast({
          title: t('common.error'),
          description: t('gameNightDetail.rsvp.errors.generic'),
          variant: 'destructive',
        }),
    });
  }

  // ────────────────────────────────────────────────────────────────────
  // Render states
  // ────────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="p-4 max-w-2xl mx-auto space-y-4">
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (isError || !event) {
    return (
      <div className="text-center py-12">
        <h2 className="font-display text-lg font-extrabold text-foreground">
          {t('gameNightDetail.errors.notFound.title')}
        </h2>
        <p className="mt-2 text-muted-foreground">{t('gameNightDetail.errors.notFound.body')}</p>
        <Button asChild className="mt-4" variant="outline">
          <Link href="/game-nights">{t('gameNightDetail.errors.notFound.cta')}</Link>
        </Button>
      </div>
    );
  }

  const isCancelled = event.status === 'Cancelled';
  const isCompleted = event.status === 'Completed';
  const isLive = event.status === 'Published';
  const hasActiveSession = activeSessions.some(s => s.status === 'in_progress');

  const statusKey = event.status.toLowerCase() as 'draft' | 'published' | 'completed' | 'cancelled';
  const statusLabel = t(`gameNightDetail.status.${statusKey}`);

  const scheduledLine = dateFormatter.format(new Date(event.scheduledAt));
  const organizedByLine = t('gameNightDetail.hero.organizedBy', { name: event.organizerName });

  const metaLine = event.maxPlayers
    ? t('gameNightDetail.hero.capacity', {
        accepted: event.acceptedCount,
        max: event.maxPlayers,
      })
    : t('gameNightDetail.hero.capacityUncapped', { accepted: event.acceptedCount });

  const heroLabels = {
    statusLabel,
    scheduledLine,
    organizedByLine,
    metaLine,
    ...(event.location ? { locationLine: event.location } : {}),
  };

  // For non-Draft branches the planning layout is irrelevant.
  const availableGames = (catalogData?.items ?? []).map(g => ({
    id: g.id,
    title: g.title,
    thumbnailUrl: g.thumbnailUrl ?? undefined,
    minPlayers: g.minPlayers,
    maxPlayers: g.maxPlayers,
  }));

  const isHost = actor?.actor === 'host';
  const isGuest = actor?.actor === 'guest';

  // Sort roster: host first, then me, then by status priority.
  const sortedRsvps = rsvps
    ? rsvps.slice().sort((a, b) => {
        if (a.userId === event.organizerId) return -1;
        if (b.userId === event.organizerId) return 1;
        if (a.userId === viewer?.id) return -1;
        if (b.userId === viewer?.id) return 1;
        return 0;
      })
    : [];

  const cancelledBannerLabels = {
    title: t('gameNightDetail.cancelled.title'),
    meta: t('gameNightDetail.cancelled.meta', { organizer: event.organizerName }),
    reasonLabel: t('gameNightDetail.cancelled.reasonLabel'),
    ctaLabel: t('gameNightDetail.cancelled.ctaLabel'),
  };

  const rsvpLabels = {
    sectionTitle: t('gameNightDetail.rsvp.sectionTitle'),
    accept: t('gameNightDetail.rsvp.accept'),
    maybe: t('gameNightDetail.rsvp.maybe'),
    decline: t('gameNightDetail.rsvp.decline'),
    saving: t('gameNightDetail.rsvp.saving'),
  };

  const hostLabel = t('gameNightDetail.participants.hostLabel');

  function rsvpStatusLabel(status: string): string {
    const key = status.toLowerCase() as 'accepted' | 'declined' | 'maybe' | 'pending';
    return t(`gameNightDetail.participants.rsvpStatus.${key}`);
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-4">
      <GameNightDetailHero
        title={event.title}
        status={event.status}
        labels={heroLabels}
        organizerId={event.organizerId}
        organizerName={event.organizerName}
      />

      {/* Host action row — kept compact + ungrouped with the hero. */}
      {isHost && (isDraft || (!isCancelled && !isCompleted)) && (
        <div className="flex justify-end gap-2">
          {isDraft && (
            <>
              <Button size="sm" variant="outline" asChild>
                <Link href={`/game-nights/${id}/edit`}>
                  <Edit className="mr-1 h-4 w-4" />
                  {t('gameNightDetail.actor.host.edit')}
                </Link>
              </Button>
              <Button size="sm" onClick={handlePublish} disabled={publishMutation.isPending}>
                <Send className="mr-1 h-4 w-4" />
                {t('gameNightDetail.actor.host.publish')}
              </Button>
            </>
          )}
          {!isDraft && !isCancelled && !isCompleted && (
            <Button
              size="sm"
              variant="destructive"
              onClick={handleCancel}
              disabled={cancelMutation.isPending}
            >
              <XCircle className="mr-1 h-4 w-4" />
              {t('gameNightDetail.actor.host.cancel')}
            </Button>
          )}
        </div>
      )}

      {isCancelled && (
        <GameNightCancelledBanner
          labels={cancelledBannerLabels}
          reason={event.description}
          onCreateNew={() => router.push('/game-nights/new')}
        />
      )}

      {/* Draft → preserve legacy planning layout. */}
      {isDraft && <GameNightPlanningLayout title={event.title} availableGames={availableGames} />}

      {/* RSVP action bar — guests only, on Published events. */}
      {isGuest && isLive && (
        <GameNightRsvpActionBar
          labels={rsvpLabels}
          currentResponse={currentResponse}
          pendingResponse={pendingResponse}
          onSelect={handleRsvp}
        />
      )}

      {/* Live / Completed: session flow sections under hero. */}
      {(isLive || isCompleted) && (
        <>
          <GameNightActions
            gameNightId={id}
            hasActiveSession={hasActiveSession}
            sessionCount={activeSessions.length}
            isCompleted={isCompleted}
          />
          <GameNightSessionsList sessions={activeSessions} gameNightId={id} />
          <GameNightDiaryPanel gameNightId={id} />
        </>
      )}

      {/* Roster */}
      {sortedRsvps.length > 0 && (
        <section
          aria-label={t('gameNightDetail.participants.sectionTitle', {
            count: sortedRsvps.length,
          })}
          className="space-y-2"
        >
          <h2 className="font-display text-base font-extrabold text-foreground">
            {t('gameNightDetail.participants.sectionTitle', { count: sortedRsvps.length })}
          </h2>
          <ul className="space-y-2">
            {sortedRsvps.map(rsvp => (
              <li key={rsvp.id}>
                <GameNightRsvpRow
                  userId={rsvp.userId}
                  userName={rsvp.userName}
                  status={rsvp.status}
                  statusLabel={rsvpStatusLabel(rsvp.status)}
                  isMe={rsvp.userId === viewer?.id}
                  isHost={rsvp.userId === event.organizerId}
                  hostLabel={hostLabel}
                />
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
