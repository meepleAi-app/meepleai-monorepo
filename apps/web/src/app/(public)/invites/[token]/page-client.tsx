/**
 * /invites/[token] — client body (V2, Wave A.5b, Issue #611).
 *
 * Owns:
 *   - i18n resolution via `useTranslation()`
 *   - 7-state FSM derivation: `default | logged-in | accepted-success |
 *     declined | token-expired | token-invalid | already-accepted`
 *   - Mutation lifecycle for accept/decline + idempotency D2(b) handling
 *     (200 same-state, 409 state-switch, 410 Gone)
 *   - `?state=...` visual-test override gated by NODE_ENV / visual-test build
 *   - Auth card shell + desktop split layout INLINE (YAGNI per spec):
 *     centered card on mobile, two-column on `lg:` breakpoint without
 *     extracting a one-time helper component.
 *   - Game thumbnail INLINE (YAGNI): tiny avatar adjacent to game name in
 *     hero, no MeepleCard variant indirection for a 32px thumbnail.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-accept-invite.jsx`
 * Spec: `docs/superpowers/specs/2026-04-29-v2-migration-wave-a-5b-invites-token.md`
 */

'use client';

import { useCallback, useMemo, useState, type JSX } from 'react';

import Image from 'next/image';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';

import {
  AcceptedSuccessShell,
  DeclinedShell,
  ErrorBanner,
  InviteHero,
  InviteHostCard,
  SessionMetaGrid,
  type SessionMetaItem,
} from '@/components/ui/v2/invites';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useGameNightInvitation } from '@/hooks/useGameNightInvitation';
import { useRespondToInvitation } from '@/hooks/useRespondToInvitation';
import { useTranslation } from '@/hooks/useTranslation';
import { type PublicGameNightInvitation, type RsvpAction } from '@/lib/api/game-night-invitations';

// --------------------------------------------------------------------------
// FSM
// --------------------------------------------------------------------------

type InviteState =
  | 'default'
  | 'logged-in'
  | 'accepted-success'
  | 'declined'
  | 'token-expired'
  | 'token-invalid'
  | 'already-accepted';

const VALID_STATE_OVERRIDES: ReadonlySet<InviteState> = new Set([
  'default',
  'logged-in',
  'accepted-success',
  'declined',
  'token-expired',
  'token-invalid',
  'already-accepted',
]);

const IS_NON_PROD = process.env.NODE_ENV !== 'production';
const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';
const STATE_OVERRIDE_ENABLED = IS_NON_PROD || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): InviteState | undefined {
  if (!STATE_OVERRIDE_ENABLED || !raw) return undefined;
  return VALID_STATE_OVERRIDES.has(raw as InviteState) ? (raw as InviteState) : undefined;
}

interface DeriveStateArgs {
  readonly data: PublicGameNightInvitation | undefined;
  readonly hasSession: boolean;
  readonly mutationKind: 'success' | 'conflict-state-switch' | 'gone' | null;
  readonly mutationAction: RsvpAction | null;
  readonly initialBannerState: 'token-invalid' | undefined;
  readonly stateOverride: InviteState | undefined;
}

function deriveState({
  data,
  hasSession,
  mutationKind,
  mutationAction,
  initialBannerState,
  stateOverride,
}: DeriveStateArgs): InviteState {
  // 1. Visual-test override (dev/CI only).
  if (stateOverride) return stateOverride;

  // 2. SSR-provided structural error.
  if (initialBannerState === 'token-invalid') return 'token-invalid';

  // 3. Mutation-driven transitions outrank stale GET data — they reflect the
  //    user's last interaction.
  if (mutationKind === 'success' && mutationAction === 'Accepted') return 'accepted-success';
  if (mutationKind === 'success' && mutationAction === 'Declined') return 'declined';
  if (mutationKind === 'gone') return 'token-expired';
  // (Conflict surfaces as a banner overlaid on the existing surface; it does
  //  not transition the FSM. Caller derives the banner separately.)

  // 4. Server-side status snapshots (SSR seed or refetch).
  if (!data) {
    // No SSR data, no token-invalid signal — render the default surface and
    // let the client query retry. The hook surfaces a generic error banner
    // if it also fails.
    return hasSession ? 'logged-in' : 'default';
  }
  if (data.status === 'Expired' || data.status === 'Cancelled') return 'token-expired';
  if (data.alreadyRespondedAs === 'Accepted' || data.alreadyRespondedAs === 'Declined') {
    return 'already-accepted';
  }

  // 5. Pending invitation. Distinguish anonymous vs logged-in for pre-fill UX.
  return hasSession ? 'logged-in' : 'default';
}

// --------------------------------------------------------------------------
// Avatar helpers (deterministic, derived from server fields).
// --------------------------------------------------------------------------

/**
 * DJB2 → hue 0-360. Deterministic from `hostUserId` so the panel/avatar tint
 * is stable across reloads and matches any backend-side derivation if we
 * ever expose the hue directly. Cheap (~10 chars × 1 mul each), no need to
 * memoize.
 */
function hueFromHostId(hostUserId: string): number {
  let hash = 5381;
  for (let i = 0; i < hostUserId.length; i++) {
    hash = (hash * 33) ^ hostUserId.charCodeAt(i);
  }
  // Squash to non-negative integer then 0-359.
  return Math.abs(hash) % 360;
}

/** Up-to-2-char initials from a display name. Falls back to `?` for empty input. */
function initialsFrom(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

// --------------------------------------------------------------------------
// Component
// --------------------------------------------------------------------------

export interface InvitesTokenPageClientProps {
  readonly token: string;
  readonly initialData?: PublicGameNightInvitation;
  readonly initialBannerState?: 'token-invalid';
}

export function InvitesTokenPageClient({
  token,
  initialData,
  initialBannerState,
}: InvitesTokenPageClientProps): JSX.Element {
  const { t, formatDate, formatTime, formatMessage } = useTranslation();
  const searchParams = useSearchParams();
  const stateOverride = parseStateOverride(searchParams?.get('state') ?? null);

  // Auth detection (client-side; no SSR session reuse for guest-by-default
  // invite UX).
  const { user } = useAuthUser();
  const hasSession = Boolean(user);

  // SSR-seeded query.
  const { data, refetch } = useGameNightInvitation({ token, initialData });
  const invitation = data ?? initialData;

  // Mutation FSM.
  const respond = useRespondToInvitation({ token });
  const mutationKind = respond.result?.kind ?? null;
  const [lastSubmittedAction, setLastSubmittedAction] = useState<RsvpAction | null>(null);

  const handleAccept = useCallback(async (): Promise<void> => {
    setLastSubmittedAction('Accepted');
    const result = await respond.submit('Accepted');
    if (result?.kind === 'conflict-state-switch') {
      // Refetch to surface fresh `alreadyRespondedAs` so the banner copy
      // matches the server state (not just our optimistic view).
      await refetch();
    }
  }, [refetch, respond]);

  const handleDecline = useCallback(async (): Promise<void> => {
    setLastSubmittedAction('Declined');
    const result = await respond.submit('Declined');
    if (result?.kind === 'conflict-state-switch') {
      await refetch();
    }
  }, [refetch, respond]);

  const handleUndoDecline = useCallback((): void => {
    // Spec §3.3.4: undo is a re-mutation, not a UI-only toggle. Backend will
    // either accept (200), conflict-switch (409), or 410 — same handlers.
    void handleAccept();
  }, [handleAccept]);

  // ---- Derive surface state ---------------------------------------------
  const state = useMemo(
    () =>
      deriveState({
        data: invitation,
        hasSession,
        mutationKind,
        mutationAction: lastSubmittedAction,
        initialBannerState,
        stateOverride,
      }),
    [invitation, hasSession, mutationKind, lastSubmittedAction, initialBannerState, stateOverride]
  );

  // ---- Resolve session meta items (i18n + format upfront) ---------------
  const sessionMeta: readonly SessionMetaItem[] = useMemo(() => {
    if (!invitation) return [];
    const scheduledDate = new Date(invitation.scheduledAt);
    const dateLabel = formatDate(scheduledDate, {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      weekday: 'short',
    });
    const timeLabel = formatTime(scheduledDate, {
      hour: '2-digit',
      minute: '2-digit',
    });

    const items: SessionMetaItem[] = [
      {
        key: 'date',
        icon: '📅',
        label: t('pages.invites.session.dateLabel'),
        value: dateLabel,
      },
      {
        key: 'time',
        icon: '🕖',
        label: t('pages.invites.session.timeLabel'),
        value: timeLabel,
        mono: true,
      },
    ];

    if (invitation.durationMinutes != null) {
      items.push({
        key: 'duration',
        icon: '⏱️',
        label: t('pages.invites.session.durationLabel'),
        value: formatMessage(
          { id: 'pages.invites.session.durationValue' },
          { minutes: invitation.durationMinutes }
        ),
        mono: true,
      });
    }

    items.push({
      key: 'location',
      icon: '📍',
      label: t('pages.invites.session.locationLabel'),
      value: invitation.location ?? t('pages.invites.session.locationMissing'),
    });

    items.push({
      key: 'players',
      icon: '👥',
      label: t('pages.invites.session.playersLabel'),
      value: t('pages.invites.session.playersValue', {
        accepted: invitation.acceptedSoFar,
        expected: invitation.expectedPlayers,
      }),
    });

    return items;
  }, [invitation, formatDate, formatTime, formatMessage, t]);

  // ---- Derive header pieces ---------------------------------------------
  const heroDate = useMemo(() => {
    if (!invitation) return '';
    return formatDate(new Date(invitation.scheduledAt), {
      day: 'numeric',
      month: 'short',
      weekday: 'short',
    });
  }, [invitation, formatDate]);
  const heroTime = useMemo(() => {
    if (!invitation) return '';
    return formatTime(new Date(invitation.scheduledAt), {
      hour: '2-digit',
      minute: '2-digit',
    });
  }, [invitation, formatTime]);

  const hostHue = invitation ? hueFromHostId(invitation.hostUserId) : 0;
  const hostInitials = invitation ? initialsFrom(invitation.hostDisplayName) : '';

  // ---- Conflict / gone banner derivation (overlay on top of FSM) --------
  const conflictBanner =
    respond.state === 'conflict' && respond.result?.kind === 'conflict-state-switch'
      ? {
          tone: 'warning' as const,
          children: t('pages.invites.banners.stateSwitchConflict', {
            currentResponse:
              respond.result.currentlyRespondedAs === 'Accepted'
                ? t('pages.invites.states.alreadyAccepted.subTextAccepted')
                : t('pages.invites.states.alreadyAccepted.subTextDeclined'),
          }),
        }
      : null;

  const errorBanner =
    respond.state === 'error'
      ? { tone: 'error' as const, children: t('pages.invites.banners.errorGeneric') }
      : null;

  // ---- Render -----------------------------------------------------------
  return (
    <main
      data-slot="invites-token-page"
      data-state={state}
      aria-label={t('pages.invites.states.default.ariaRegion')}
      className="relative isolate min-h-screen bg-[hsl(var(--bg-app))]"
    >
      <div className="mx-auto grid w-full max-w-[1080px] gap-8 px-4 py-8 lg:grid-cols-[1fr_minmax(0,420px)] lg:items-start lg:gap-12 lg:px-8 lg:py-16">
        {/* Desktop-only brand panel — hidden on mobile (auth-card-shell pattern, inlined per YAGNI). */}
        <aside
          aria-hidden="true"
          className="hidden flex-col justify-center gap-3 lg:flex"
          data-slot="invites-token-brand"
        >
          <span className="font-mono text-[10px] font-bold uppercase tracking-[0.16em] text-[hsl(var(--text-muted))]">
            MeepleAI
          </span>
          <h2 className="font-display text-[28px] font-extrabold leading-tight tracking-[-0.01em] text-foreground">
            {t('pages.invites.metadata.title')}
          </h2>
          <p className="text-[13px] leading-relaxed text-[hsl(var(--text-sec))]">
            {t('pages.invites.metadata.description')}
          </p>
        </aside>

        {/* Auth-card-shell — centered on mobile, anchored right on desktop. */}
        <section
          data-slot="invites-token-card"
          className="mx-auto w-full max-w-[420px] overflow-hidden rounded-2xl border bg-[hsl(var(--bg-card))] shadow-sm"
        >
          {state === 'token-invalid' ? (
            <TokenInvalidShell t={t} />
          ) : state === 'token-expired' ? (
            <TokenExpiredShell t={t} hostName={invitation?.hostDisplayName ?? ''} />
          ) : state === 'accepted-success' && invitation ? (
            <AcceptedSuccessSurface
              invitation={invitation}
              heroDate={heroDate}
              heroTime={heroTime}
              t={t}
            />
          ) : state === 'declined' && invitation ? (
            <DeclinedSurface
              invitation={invitation}
              onUndo={handleUndoDecline}
              isSubmitting={respond.state === 'submitting'}
              t={t}
            />
          ) : state === 'already-accepted' && invitation ? (
            <AlreadyRespondedSurface
              invitation={invitation}
              hostHue={hostHue}
              hostInitials={hostInitials}
              heroDate={heroDate}
              heroTime={heroTime}
              sessionMeta={sessionMeta}
              t={t}
            />
          ) : invitation ? (
            <PendingSurface
              invitation={invitation}
              hostHue={hostHue}
              hostInitials={hostInitials}
              heroDate={heroDate}
              heroTime={heroTime}
              sessionMeta={sessionMeta}
              hasSession={hasSession}
              userDisplayName={user?.displayName ?? user?.email ?? ''}
              isSubmitting={respond.state === 'submitting'}
              submittingAction={lastSubmittedAction}
              conflictBanner={conflictBanner}
              errorBanner={errorBanner}
              onAccept={handleAccept}
              onDecline={handleDecline}
              t={t}
            />
          ) : (
            // Defensive empty fallback — SSR provided no data and no
            // structural error. Surface is briefly empty until client retry.
            <div
              data-slot="invites-token-loading"
              aria-busy="true"
              aria-live="polite"
              className="px-6 py-12 text-center text-[12px] text-[hsl(var(--text-muted))]"
            >
              …
            </div>
          )}
        </section>
      </div>
    </main>
  );
}

// --------------------------------------------------------------------------
// State surfaces (kept inline — collocated with FSM, no cross-page reuse).
// --------------------------------------------------------------------------

type T = ReturnType<typeof useTranslation>['t'];

interface PendingSurfaceProps {
  readonly invitation: PublicGameNightInvitation;
  readonly hostHue: number;
  readonly hostInitials: string;
  readonly heroDate: string;
  readonly heroTime: string;
  readonly sessionMeta: readonly SessionMetaItem[];
  readonly hasSession: boolean;
  readonly userDisplayName: string;
  readonly isSubmitting: boolean;
  readonly submittingAction: RsvpAction | null;
  readonly conflictBanner: { tone: 'warning'; children: string } | null;
  readonly errorBanner: { tone: 'error'; children: string } | null;
  readonly onAccept: () => void;
  readonly onDecline: () => void;
  readonly t: T;
}

function PendingSurface({
  invitation,
  hostHue,
  hostInitials,
  heroDate,
  heroTime,
  sessionMeta,
  hasSession,
  userDisplayName,
  isSubmitting,
  submittingAction,
  conflictBanner,
  errorBanner,
  onAccept,
  onDecline,
  t,
}: PendingSurfaceProps): JSX.Element {
  return (
    <>
      <InviteHero
        eyebrowText={t('pages.invites.metadata.title')}
        hostName={invitation.hostDisplayName}
        gameName={invitation.primaryGameName ?? ''}
        inviteVerb="ti invita a giocare a"
        dateShort={heroDate}
        time={heroTime}
      />
      <div className="flex flex-col gap-3.5 px-4 pb-5">
        {/* Game thumbnail INLINE per YAGNI (32px square) — appears only when image present. */}
        {invitation.primaryGameImageUrl && invitation.primaryGameName ? (
          <div
            data-slot="invites-token-game-thumb"
            className="flex items-center gap-2.5 rounded-md border border-[hsl(var(--border-light))] bg-[hsl(var(--bg-muted))] px-3 py-2"
          >
            <Image
              src={invitation.primaryGameImageUrl}
              alt=""
              width={32}
              height={32}
              className="h-8 w-8 flex-shrink-0 rounded object-cover"
              unoptimized
            />
            <span className="font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
              {t('pages.invites.session.primaryGameLabel')}
            </span>
            <span className="flex-1 text-right font-display text-[13px] font-bold text-foreground">
              {invitation.primaryGameName}
            </span>
          </div>
        ) : null}

        <InviteHostCard
          hostDisplayName={invitation.hostDisplayName}
          hostInitials={hostInitials}
          hostHue={hostHue}
          welcomeMessage={invitation.hostWelcomeMessage}
          hostLabel="Host"
          noMessageLabel={t('pages.invites.host.welcomeMessageLabel')}
          avatarAriaLabel={t('pages.invites.host.ariaAvatarFallback', {
            name: invitation.hostDisplayName,
          })}
        />

        <SessionMetaGrid items={sessionMeta} />

        {hasSession ? (
          <p
            data-slot="invites-token-prefill-badge"
            className="mt-1 rounded-md bg-[hsl(var(--c-info)/0.10)] px-3 py-2 text-[11px] text-[hsl(var(--c-info))]"
          >
            {t('pages.invites.states.loggedIn.preFillBadge', { name: userDisplayName })}
          </p>
        ) : null}

        {conflictBanner ? (
          <ErrorBanner tone={conflictBanner.tone}>{conflictBanner.children}</ErrorBanner>
        ) : null}
        {errorBanner ? (
          <ErrorBanner tone={errorBanner.tone}>{errorBanner.children}</ErrorBanner>
        ) : null}

        <div className="flex flex-col gap-2 pt-1">
          <button
            type="button"
            onClick={onAccept}
            disabled={isSubmitting}
            data-slot="invites-token-accept-cta"
            className="inline-flex w-full items-center justify-center rounded-md border-0 bg-[hsl(var(--c-toolkit))] px-4 py-3 font-display text-[14px] font-bold text-[hsl(var(--c-toolkit-foreground,0_0%_100%))] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting && submittingAction === 'Accepted'
              ? t('pages.invites.form.submittingAccept')
              : hasSession && userDisplayName.length > 0
                ? t('pages.invites.form.acceptCtaLoggedIn', { displayName: userDisplayName })
                : t('pages.invites.form.acceptCta')}
          </button>
          <button
            type="button"
            onClick={onDecline}
            disabled={isSubmitting}
            data-slot="invites-token-decline-cta"
            className="inline-flex w-full items-center justify-center rounded-md border bg-[hsl(var(--bg-muted))] px-4 py-2.5 font-display text-[13px] font-semibold text-foreground disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting && submittingAction === 'Declined'
              ? t('pages.invites.form.submittingDecline')
              : t('pages.invites.form.declineCta')}
          </button>
        </div>
      </div>
    </>
  );
}

interface AcceptedSuccessSurfaceProps {
  readonly invitation: PublicGameNightInvitation;
  readonly heroDate: string;
  readonly heroTime: string;
  readonly t: T;
}

function AcceptedSuccessSurface({
  invitation,
  heroDate,
  heroTime,
  t,
}: AcceptedSuccessSurfaceProps): JSX.Element {
  const summaryMeta = `${heroDate} · ${heroTime}${invitation.location ? ` · ${invitation.location}` : ''}`;
  return (
    <div className="px-4 py-5">
      <AcceptedSuccessShell
        headline={t('pages.invites.states.acceptedSuccess.heading')}
        subText={t('pages.invites.states.acceptedSuccess.subText', {
          hostName: invitation.hostDisplayName,
        })}
        summaryLabel="Riepilogo"
        gameName={invitation.primaryGameName ?? '—'}
        summaryMeta={summaryMeta}
        icsLabel={t('pages.invites.states.acceptedSuccess.addToCalendarCta')}
      />
    </div>
  );
}

interface DeclinedSurfaceProps {
  readonly invitation: PublicGameNightInvitation;
  readonly onUndo: () => void;
  readonly isSubmitting: boolean;
  readonly t: T;
}

function DeclinedSurface({
  invitation,
  onUndo,
  isSubmitting,
  t,
}: DeclinedSurfaceProps): JSX.Element {
  return (
    <div className="px-4 py-5">
      <DeclinedShell
        headline={t('pages.invites.states.declined.heading')}
        subText={t('pages.invites.states.declined.subText', {
          hostName: invitation.hostDisplayName,
        })}
        returnHomeLabel={t('pages.invites.states.tokenInvalid.ctaHome')}
        undoLabel={
          isSubmitting
            ? t('pages.invites.form.submittingAccept')
            : t('pages.invites.states.declined.changeMyMindCta')
        }
        onUndo={isSubmitting ? undefined : onUndo}
      />
    </div>
  );
}

interface TokenExpiredShellProps {
  readonly t: T;
  readonly hostName: string;
}

function TokenExpiredShell({ t, hostName }: TokenExpiredShellProps): JSX.Element {
  return (
    <div className="flex flex-col items-center px-5 py-8 text-center">
      <div
        aria-hidden="true"
        className="mb-3.5 flex h-[60px] w-[60px] items-center justify-center rounded-full border bg-[hsl(var(--bg-muted))] text-[26px]"
      >
        ⌛
      </div>
      <h2 className="m-0 mb-1.5 font-display text-[18px] font-bold text-foreground">
        {t('pages.invites.states.tokenExpired.heading')}
      </h2>
      <p className="m-0 mb-4 text-[12px] leading-relaxed text-[hsl(var(--text-muted))]">
        {hostName.length > 0
          ? t('pages.invites.states.tokenExpired.subText', { hostName })
          : t('pages.invites.banners.goneExpired')}
      </p>
      <Link
        href="/"
        className="inline-flex items-center justify-center rounded-md border bg-[hsl(var(--bg-muted))] px-3.5 py-2.5 font-display text-[13px] font-bold text-foreground no-underline"
      >
        {t('pages.invites.states.tokenInvalid.ctaHome')}
      </Link>
    </div>
  );
}

function TokenInvalidShell({ t }: { readonly t: T }): JSX.Element {
  return (
    <div className="flex flex-col items-center px-5 py-8 text-center">
      <div
        aria-hidden="true"
        className="mb-3.5 flex h-[60px] w-[60px] items-center justify-center rounded-full border bg-[hsl(var(--bg-muted))] text-[26px]"
      >
        🔍
      </div>
      <h2 className="m-0 mb-1.5 font-display text-[18px] font-bold text-foreground">
        {t('pages.invites.states.tokenInvalid.heading')}
      </h2>
      <p className="m-0 mb-4 text-[12px] leading-relaxed text-[hsl(var(--text-muted))]">
        {t('pages.invites.states.tokenInvalid.subText')}
      </p>
      <Link
        href="/"
        className="inline-flex items-center justify-center rounded-md border bg-[hsl(var(--bg-muted))] px-3.5 py-2.5 font-display text-[13px] font-bold text-foreground no-underline"
      >
        {t('pages.invites.states.tokenInvalid.ctaHome')}
      </Link>
    </div>
  );
}

interface AlreadyRespondedSurfaceProps {
  readonly invitation: PublicGameNightInvitation;
  readonly hostHue: number;
  readonly hostInitials: string;
  readonly heroDate: string;
  readonly heroTime: string;
  readonly sessionMeta: readonly SessionMetaItem[];
  readonly t: T;
}

function AlreadyRespondedSurface({
  invitation,
  hostHue,
  hostInitials,
  heroDate,
  heroTime,
  sessionMeta,
  t,
}: AlreadyRespondedSurfaceProps): JSX.Element {
  const wasAccepted = invitation.alreadyRespondedAs === 'Accepted';
  return (
    <>
      <InviteHero
        eyebrowText={t('pages.invites.metadata.title')}
        hostName={invitation.hostDisplayName}
        gameName={invitation.primaryGameName ?? ''}
        inviteVerb="ti invita a giocare a"
        dateShort={heroDate}
        time={heroTime}
      />
      <div className="flex flex-col gap-3.5 px-4 pb-5">
        <ErrorBanner tone="info">
          <strong className="font-bold">{t('pages.invites.states.alreadyAccepted.heading')}</strong>
          {' — '}
          {wasAccepted
            ? t('pages.invites.states.alreadyAccepted.subTextAccepted')
            : t('pages.invites.states.alreadyAccepted.subTextDeclined')}
          {' · '}
          {t('pages.invites.states.alreadyAccepted.rosterCount', {
            accepted: invitation.acceptedSoFar,
            expected: invitation.expectedPlayers,
          })}
        </ErrorBanner>
        <InviteHostCard
          hostDisplayName={invitation.hostDisplayName}
          hostInitials={hostInitials}
          hostHue={hostHue}
          welcomeMessage={invitation.hostWelcomeMessage}
          hostLabel="Host"
          noMessageLabel={t('pages.invites.host.welcomeMessageLabel')}
          avatarAriaLabel={t('pages.invites.host.ariaAvatarFallback', {
            name: invitation.hostDisplayName,
          })}
        />
        <SessionMetaGrid items={sessionMeta} />
      </div>
    </>
  );
}
