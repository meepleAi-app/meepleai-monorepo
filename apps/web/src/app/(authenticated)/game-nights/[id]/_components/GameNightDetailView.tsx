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
 * Mobile sticky CTA (#2989 Screen C): on <md the guest RsvpActionBar and the
 * Draft host "Invia inviti" CTA are lifted into a thumb-reachable sticky bottom
 * bar (MobileStickyBar) pinned above the fixed MobileBottomBar; both collapse
 * to inline document flow at md+. Supersedes the earlier decision-3c deferral.
 *
 * Error→toast mapping follows the rsvp-state-machine + HTTP-status keys defined
 * under `gameNightDetail.rsvp.errors.*` in the i18n bundle.
 */

'use client';

import { useEffect, useMemo } from 'react';

import { Edit, Send, XCircle } from 'lucide-react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import {
  GameNightCancelledBanner,
  GameNightDetailHero,
  GameNightRsvpActionBar,
  GameNightRsvpRow,
} from '@/components/features/game-night-detail';
import { VotingPanel } from '@/components/features/game-night-detail/voting/VotingPanel';
import { GameNightActions } from '@/components/game-night/GameNightActions';
import { GameNightDiaryPanel } from '@/components/game-night/GameNightDiaryPanel';
import { GameNightSessionsList } from '@/components/game-night/GameNightSessionsList';
import { GameNightPlanningLayout } from '@/components/game-night/planning/GameNightPlanningLayout';
import { FormPageContainer } from '@/components/layout/PageContainer';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useGameNightDetail } from '@/hooks/queries/useGameNightDetail';
import { useCancelGameNight, usePublishGameNight } from '@/hooks/queries/useGameNights';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import { useNetworkStatus } from '@/hooks/useNetworkStatus';
import { useToast } from '@/hooks/useToast';
import { useTranslation } from '@/hooks/useTranslation';
import type { GameNightStatus } from '@/lib/api/schemas/game-nights.schemas';
import type { RsvpResponse } from '@/lib/game-nights/rsvp-state-machine';
import { cn } from '@/lib/utils';
import { useGameNightStore } from '@/stores/game-night';

import { GameNightEditDrawer } from './GameNightEditDrawer';

/**
 * Mobile-only sticky action bar (#2989 Screen C). Pins its children just above
 * the fixed MobileBottomBar (`bottom-16` ≈ the shell's `pb-16` clearance, which
 * also owns the screen-edge safe-area) at <md and collapses to normal document
 * flow at md+. `z-30` keeps it above page content but below the `z-40`
 * MobileBottomBar. Callers pass a surface `className` (glass / border) — the
 * children may be bare controls that need a backdrop.
 */
function MobileStickyBar({
  testId,
  className,
  children,
}: {
  testId: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div
      data-testid={testId}
      className={cn(
        'fixed inset-x-0 bottom-16 z-30 p-3',
        'md:static md:inset-x-auto md:bottom-auto md:z-auto md:p-0',
        className
      )}
    >
      {children}
    </div>
  );
}

export function GameNightDetailView({ id }: { id: string }): React.JSX.Element {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t, locale } = useTranslation();
  const { toast } = useToast();
  // #2989 gap 5: RSVP CTAs are disabled while offline (can't reach the server).
  const { isOffline } = useNetworkStatus();

  const { data: viewer } = useCurrentUser();
  const detail = useGameNightDetail(id, viewer?.id);
  const publishMutation = usePublishGameNight();
  const cancelMutation = useCancelGameNight();

  const { event, rsvps, actor, isLoading, isError, currentResponse, pendingResponse } = detail;

  // Catalog games power the Draft planning layout and the Published voting-panel titles.
  const isDraft = event?.status === 'Draft';
  const needsCatalog = isDraft || event?.status === 'Published';
  const { data: catalogData } = useSharedGames(undefined, needsCatalog);

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
    // Toasts fire from the mutation callbacks below — NOT on the synchronous
    // 'submitted' return — so a server rejection produces an error toast
    // instead of a stale success toast. #1171 review fix.
    const outcome = detail.submitRsvp(response, {
      onSuccess: submitted => {
        const toastKey: Record<RsvpResponse, string> = {
          Accepted: 'confirmedToast',
          Maybe: 'maybeToast',
          Declined: 'declinedToast',
        };
        toast({ title: t(`gameNightDetail.rsvp.${toastKey[submitted]}`) });
      },
      onError: () => {
        toast({
          title: t('common.error'),
          description: t('gameNightDetail.rsvp.errors.generic'),
          variant: 'destructive',
        });
      },
    });
    if (outcome.kind === 'rejected') {
      const errorKey = outcome.status === 410 ? 'cancelledGone' : 'directConflict';
      toast({
        title: t('common.error'),
        description: t(`gameNightDetail.rsvp.errors.${errorKey}`),
        variant: 'destructive',
      });
    }
  }

  function handlePublish() {
    publishMutation.mutate(id, {
      onSuccess: () =>
        toast({
          title: t('gameNightDetail.actor.host.publishedToast'),
          description: t('gameNightDetail.actor.host.publishedToastDescription'),
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
      onSuccess: () => toast({ title: t('gameNightDetail.actor.host.cancelledToast') }),
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
      <FormPageContainer className="p-4 space-y-4">
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-32 w-full" />
      </FormPageContainer>
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

  // #2723: Published events surface a Dettagli|Votazione tab strip (ADR-061 tab-canonical),
  // deep-linked via ?tab=voting. The voting tab shows only the VotingPanel; the details tab
  // shows the RSVP / session / roster body.
  const votingTabActive = isLive && searchParams.get('tab') === 'voting';
  const showDetailsContent = !votingTabActive;
  const hasActiveSession = activeSessions.some(s => s.status === 'in_progress');

  // Explicit status → i18n key map. Deriving the key via toLowerCase() produced
  // 'inprogress' for the 'InProgress' status, which did not match the camelCase
  // locale key 'inProgress' and leaked the raw key to the badge (#3263 discovery).
  // Record<GameNightStatus, …> makes a future enum addition fail to compile.
  const statusLabelKey: Record<GameNightStatus, string> = {
    Draft: 'draft',
    Published: 'published',
    InProgress: 'inProgress',
    Completed: 'completed',
    Cancelled: 'cancelled',
  };
  const statusLabel = t(`gameNightDetail.status.${statusLabelKey[event.status]}`);

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

  // Candidate-game titles for the voting panel (#2700).
  const gameTitleById: Record<string, string> = Object.fromEntries(
    (catalogData?.items ?? []).map(g => [g.id, g.title])
  );

  const isHost = actor?.actor === 'host';
  const isGuest = actor?.actor === 'guest';

  // #2989 Screen C: a mobile sticky action bar (guest RSVP or Draft host CTA)
  // is `fixed` and overlays the bottom of the scroll region, so the container
  // reserves extra bottom padding below md so the last content is not occluded.
  // The reservation must exceed the compact bar's height (≈120px incl. 2-row
  // button wrap) — pb-40 (160px). `sm:pb-40` is required because the container's
  // inherited PADDING_DEFAULT (`sm:py-8`) is a distinct variant group that
  // tailwind-merge keeps, and it would otherwise clobber the bottom padding to
  // 32px in the 640–767px band where the bar is still fixed.
  const hasMobileStickyBar = (isGuest && isLive && showDetailsContent) || (isHost && isDraft);
  const stickyBarClearance = 'pb-40 sm:pb-40 md:pb-4';

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

  // #2963 / invariant #16: on a Draft night the roster shows *tagged* players
  // (silently added at creation, no invite sent yet). Only after the host hits
  // "Invia inviti" (publish → Published) do Pending rows read as *invited,
  // awaiting reply*. The RSVP status itself is Pending in both cases — the
  // distinction is the parent event status, so it is resolved here at the caller.
  function rsvpStatusLabel(status: string, tagged: boolean): string {
    const key = status.toLowerCase() as 'accepted' | 'declined' | 'maybe' | 'pending';
    if (tagged && key === 'pending') {
      return t('gameNightDetail.participants.rsvpStatus.tagged');
    }
    return t(`gameNightDetail.participants.rsvpStatus.${key}`);
  }

  return (
    <FormPageContainer
      data-testid="game-night-detail-container"
      className={cn('space-y-6 p-4', hasMobileStickyBar && stickyBarClearance)}
    >
      <GameNightDetailHero
        title={event.title}
        status={event.status}
        labels={heroLabels}
        organizerId={event.organizerId}
        organizerName={event.organizerName}
      />

      {/* Draft host: Edit + "Invia inviti" — the primary publish CTA is lifted
          into a mobile sticky bar (#2989 Screen C) so it stays thumb-reachable;
          bare buttons get a glass surface. Collapses to the compact inline row
          at md+. */}
      {isHost && isDraft && (
        <MobileStickyBar
          testId="host-sticky-bar"
          className="flex justify-end gap-2 border-t border-border bg-card/95 backdrop-blur md:border-0 md:bg-transparent md:backdrop-blur-none"
        >
          <Button size="sm" variant="outline" asChild>
            <Link href={`/game-nights/${id}?action=edit`}>
              <Edit className="mr-1 h-4 w-4" />
              {t('gameNightDetail.actor.host.edit')}
            </Link>
          </Button>
          <Button
            size="sm"
            data-testid="publish-game-night"
            onClick={handlePublish}
            disabled={publishMutation.isPending}
          >
            <Send className="mr-1 h-4 w-4" />
            {t('gameNightDetail.actor.host.publish')}
          </Button>
        </MobileStickyBar>
      )}

      {/* Published host: destructive Cancel stays inline (top-right) — a
          delete-style action is deliberately NOT promoted to a sticky CTA. */}
      {isHost && !isDraft && !isCancelled && !isCompleted && (
        <div className="flex justify-end gap-2">
          <Button
            size="sm"
            variant="destructive"
            onClick={handleCancel}
            disabled={cancelMutation.isPending}
          >
            <XCircle className="mr-1 h-4 w-4" />
            {t('gameNightDetail.actor.host.cancel')}
          </Button>
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

      {/* Published tabs — Dettagli | Votazione (#2723, ADR-061, ?tab=voting deep-link). */}
      {isLive && (
        <nav
          className="flex gap-1 border-b border-border"
          aria-label={t('gameNightDetail.tabs.details')}
        >
          <Link
            href={`/game-nights/${id}`}
            aria-current={showDetailsContent ? 'page' : undefined}
            data-testid="tab-details"
            className={`px-4 py-2 text-sm font-semibold ${
              showDetailsContent
                ? 'border-b-2 border-primary text-foreground'
                : 'text-muted-foreground'
            }`}
          >
            {t('gameNightDetail.tabs.details')}
          </Link>
          <Link
            href={`/game-nights/${id}?tab=voting`}
            aria-current={votingTabActive ? 'page' : undefined}
            data-testid="tab-voting"
            className={`px-4 py-2 text-sm font-semibold ${
              votingTabActive
                ? 'border-b-2 border-primary text-foreground'
                : 'text-muted-foreground'
            }`}
          >
            {t('gameNightDetail.tabs.voting')}
          </Link>
        </nav>
      )}

      {/* RSVP action bar — guests only, on Published events (details tab).
          #2989 Screen C: pinned to a mobile sticky bottom bar for one-thumb
          reach; the action bar's own opaque card is the surface, so the wrapper
          only handles positioning. Collapses to inline flow at md+. */}
      {isGuest && isLive && showDetailsContent && (
        <MobileStickyBar
          testId="rsvp-sticky-bar"
          className="border-t border-border bg-card/95 backdrop-blur md:border-0 md:bg-transparent md:backdrop-blur-none"
        >
          <GameNightRsvpActionBar
            labels={rsvpLabels}
            currentResponse={currentResponse}
            pendingResponse={pendingResponse}
            onSelect={handleRsvp}
            disabled={isOffline}
            compact
          />
        </MobileStickyBar>
      )}

      {/* Candidate voting (approval model) — Issue #2700 / #2723 voting tab. */}
      {votingTabActive && (
        <VotingPanel gameNightId={id} isOrganizer={isHost} gameTitleById={gameTitleById} />
      )}

      {/* Live / Completed: session flow sections under hero (details tab). */}
      {(isLive || isCompleted) && showDetailsContent && (
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

      {/* Roster (details tab) */}
      {sortedRsvps.length > 0 && showDetailsContent && (
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
                  statusLabel={rsvpStatusLabel(rsvp.status, isDraft)}
                  isMe={rsvp.userId === viewer?.id}
                  isHost={rsvp.userId === event.organizerId}
                  hostLabel={hostLabel}
                />
              </li>
            ))}
          </ul>
        </section>
      )}

      {/* Edit-overlay drawer (ADR-079 / #2701). Self-gates on ?action=edit +
          organiser identity — the visible trigger is Draft-only, but the drawer
          is mountable for any non-terminal status via deep-link. */}
      <GameNightEditDrawer
        gameNightId={id}
        organizerId={event.organizerId}
        defaultValues={{
          title: event.title,
          description: event.description ?? undefined,
          scheduledAt: event.scheduledAt,
          location: event.location ?? undefined,
          maxPlayers: event.maxPlayers ?? undefined,
        }}
      />
    </FormPageContainer>
  );
}
