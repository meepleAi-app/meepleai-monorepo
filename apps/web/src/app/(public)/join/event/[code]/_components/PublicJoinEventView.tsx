/**
 * PublicJoinEventView — orchestrator for /join/event/[code] (issue #1169).
 *
 * Owns the full lifecycle for the anonymous public RSVP surface:
 *   - GET invitation via `useGameNightInvitation` (TanStack Query, no SSR seed)
 *   - POST response via `useRespondToInvitation` (extended with displayName +
 *     rate-limit FSM states)
 *   - Surface routing: token-invalid (404) / token-expired (410 Gone) /
 *     rate-limited (429) / generic error (5xx/network) / loading / data
 *   - Form composition: PublicRsvpForm (with optional displayName, max 120)
 *     + GameNightDetailHero (in `mode='public'`)
 *   - Already-responded confirmation surface keyed off the new
 *     `respondedByName` backend delta
 *   - Optional "Create account" CTA on successful response for guest activation
 *
 * Pure client component. No SSR session reuse: this is a public surface that
 * must work for anonymous viewers (and for authenticated users, no special
 * handling — the backend's optional-auth on POST is transparent).
 */

'use client';

import { useCallback, useMemo, type JSX } from 'react';

import Link from 'next/link';

import {
  ExpiredOrCancelledError,
  GameNightDetailHero,
  GenericError,
  InvalidTokenError,
  PublicRsvpForm,
  RateLimitedError,
  type GameNightDetailHeroLabels,
  type PublicRsvpFormLabels,
} from '@/components/features/game-night-detail';
import { useGameNightInvitation } from '@/hooks/useGameNightInvitation';
import { useRespondToInvitation } from '@/hooks/useRespondToInvitation';
import { useTranslation } from '@/hooks/useTranslation';
import {
  InvitationGoneError,
  InvitationNotFoundError,
  InvitationRateLimitedError,
  type PublicGameNightInvitation,
  type RsvpAction,
} from '@/lib/api/game-night-invitations';
import type { GameNightStatus } from '@/lib/api/schemas/game-nights.schemas';

export interface PublicJoinEventViewProps {
  readonly code: string;
}

/**
 * Internal FSM surface enum — drives the top-level switch in render(). Mapped
 * from the combined state of `useGameNightInvitation` + `useRespondToInvitation`.
 */
type Surface =
  | 'loading'
  | 'token-invalid'
  | 'token-expired'
  | 'token-cancelled'
  | 'rate-limited'
  | 'generic-error'
  | 'rsvp';

export function PublicJoinEventView({ code }: PublicJoinEventViewProps): JSX.Element {
  const { t, formatDate, formatTime, formatMessage } = useTranslation();

  const invitationQuery = useGameNightInvitation({ token: code });
  const respond = useRespondToInvitation({ token: code });

  const invitation = invitationQuery.data;

  // -- Surface derivation ---------------------------------------------------
  // Order matters: mutation-driven terminal states (gone/rate-limited) take
  // precedence over the query state when applicable. 404 from the GET is a
  // structural state, but a 410/429 from the POST overrides the cached query
  // data to push the user toward the appropriate error UI.

  const respondGone = respond.result?.kind === 'gone' ? respond.result : null;
  const respondRateLimited = respond.result?.kind === 'rate-limited' ? respond.result : null;

  const surface: Surface = useMemo(() => {
    // Mutation-driven terminal first (the user just tried to respond):
    if (respondGone) {
      return respondGone.reason === 'cancelled' ? 'token-cancelled' : 'token-expired';
    }
    if (respondRateLimited) return 'rate-limited';

    // Query-driven states:
    if (invitationQuery.error instanceof InvitationNotFoundError) return 'token-invalid';
    if (invitationQuery.error instanceof InvitationGoneError) {
      // Backend doesn't distinguish expired vs cancelled on GET 410, so
      // default to "expired" — host-side cancellation also surfaces this way.
      return 'token-expired';
    }
    if (invitationQuery.error instanceof InvitationRateLimitedError) return 'rate-limited';
    if (invitationQuery.isError) return 'generic-error';
    if (invitationQuery.isLoading) return 'loading';
    if (!invitation) return 'loading';

    // Data-driven terminal states off the DTO `status` field:
    if (invitation.status === 'Cancelled') return 'token-cancelled';
    if (invitation.status === 'Expired') return 'token-expired';

    return 'rsvp';
  }, [
    respondGone,
    respondRateLimited,
    invitationQuery.error,
    invitationQuery.isError,
    invitationQuery.isLoading,
    invitation,
  ]);

  // -- Action handlers ------------------------------------------------------

  const handleSubmit = useCallback(
    async (action: RsvpAction, displayName: string | null): Promise<void> => {
      const result = await respond.submit(action, displayName);
      // 409 conflict surfaces server-side `alreadyRespondedAs` — refetch the
      // GET so the form re-renders the "responded" confirmation panel using
      // the canonical DTO state (including the persisted respondedByName).
      if (result?.kind === 'conflict-state-switch' || result?.kind === 'success') {
        await invitationQuery.refetch();
      }
    },
    [respond, invitationQuery]
  );

  const handleRetry = useCallback(async (): Promise<void> => {
    respond.reset();
    await invitationQuery.refetch();
  }, [respond, invitationQuery]);

  // -- Top-level shell ------------------------------------------------------
  return (
    <main
      data-slot="public-join-event-page"
      data-surface={surface}
      aria-label={t('pages.gameNightJoin.metadata.title')}
      className="min-h-screen bg-background"
    >
      <div className="mx-auto flex w-full max-w-2xl flex-col gap-4 px-4 py-6 md:px-6 md:py-10">
        {/* Public banner — visual cue that this is the anonymous RSVP route. */}
        <PublicBanner message={t('pages.gameNightJoin.publicBanner')} />

        {surface === 'loading' && (
          <p
            data-slot="public-join-event-loading"
            aria-busy="true"
            aria-live="polite"
            className="rounded-lg border border-border bg-card px-4 py-8 text-center text-sm text-muted-foreground"
          >
            {t('pages.gameNightJoin.loading')}
          </p>
        )}

        {surface === 'token-invalid' && (
          <InvalidTokenError
            labels={{
              heading: t('pages.gameNightJoin.error.invalidToken.heading'),
              body: t('pages.gameNightJoin.error.invalidToken.body'),
              homeCta: t('pages.gameNightJoin.error.invalidToken.homeCta'),
            }}
          />
        )}

        {(surface === 'token-expired' || surface === 'token-cancelled') && (
          <ExpiredOrCancelledError
            kind={surface === 'token-expired' ? 'expired' : 'cancelled'}
            labels={{
              expired: {
                heading: t('pages.gameNightJoin.error.expired.heading'),
                body: t('pages.gameNightJoin.error.expired.body'),
              },
              cancelled: {
                heading: t('pages.gameNightJoin.error.cancelled.heading'),
                body: t('pages.gameNightJoin.error.cancelled.body'),
              },
              requestNewInviteCta: t('pages.gameNightJoin.error.requestNewInviteCta'),
              homeCta: t('pages.gameNightJoin.error.homeCta'),
            }}
          />
        )}

        {surface === 'rate-limited' && (
          <RateLimitedError
            retryAfterSeconds={
              respondRateLimited?.retryAfter ??
              (invitationQuery.error instanceof InvitationRateLimitedError
                ? invitationQuery.error.retryAfter
                : null)
            }
            labels={{
              heading: t('pages.gameNightJoin.error.rateLimited.heading'),
              body: t('pages.gameNightJoin.error.rateLimited.body'),
              countdown: (seconds: number) =>
                formatMessage(
                  { id: 'pages.gameNightJoin.error.rateLimited.countdown' },
                  { seconds }
                ),
              retryCta: t('pages.gameNightJoin.error.rateLimited.retryCta'),
            }}
            onRetry={() => void handleRetry()}
          />
        )}

        {surface === 'generic-error' && (
          <GenericError
            labels={{
              heading: t('pages.gameNightJoin.error.generic.heading'),
              body: t('pages.gameNightJoin.error.generic.body'),
              retryCta: t('pages.gameNightJoin.error.generic.retryCta'),
            }}
            onRetry={() => void handleRetry()}
            isRetrying={invitationQuery.isFetching}
          />
        )}

        {surface === 'rsvp' && invitation && (
          <RsvpSurface
            invitation={invitation}
            t={t}
            formatDate={formatDate}
            formatTime={formatTime}
            formatMessage={formatMessage}
            submittingAction={respond.state === 'submitting' ? lastSubmittedAction(respond) : null}
            onSubmit={handleSubmit}
            errorBanner={
              respond.state === 'error'
                ? t('pages.gameNightJoin.error.generic.body')
                : respond.result?.kind === 'invalid-display-name'
                  ? respond.result.message
                  : null
            }
          />
        )}
      </div>
    </main>
  );
}

// --------------------------------------------------------------------------
// Internal helpers + sub-surfaces (kept inline; collocated with FSM).
// --------------------------------------------------------------------------

function PublicBanner({ message }: { readonly message: string }): JSX.Element {
  return (
    <div
      data-slot="public-join-event-banner"
      role="note"
      aria-live="off"
      className="rounded-md border border-border bg-muted px-3 py-2 text-xs text-muted-foreground"
    >
      {message}
    </div>
  );
}

/**
 * Best-effort extraction of the in-flight action from `useRespondToInvitation`.
 * The hook does not expose its mutation `variables` directly, so we map from
 * the FSM state of the last `result` (post-success) — for the submitting
 * window we fall back to null and the form spinner will not pin to a button.
 * This is intentional: the actual button click site also passes the action
 * through to the form via `submittingAction` prop derived from the FSM.
 */
function lastSubmittedAction(
  respond: ReturnType<typeof useRespondToInvitation>
): RsvpAction | null {
  if (respond.result?.kind === 'success') return respond.result.action;
  if (respond.result?.kind === 'conflict-state-switch') return respond.result.attemptedAction;
  return null;
}

interface RsvpSurfaceProps {
  readonly invitation: PublicGameNightInvitation;
  readonly t: ReturnType<typeof useTranslation>['t'];
  readonly formatDate: ReturnType<typeof useTranslation>['formatDate'];
  readonly formatTime: ReturnType<typeof useTranslation>['formatTime'];
  readonly formatMessage: ReturnType<typeof useTranslation>['formatMessage'];
  readonly submittingAction: RsvpAction | null;
  readonly onSubmit: (action: RsvpAction, displayName: string | null) => void;
  readonly errorBanner: string | null;
}

function RsvpSurface({
  invitation,
  t,
  formatDate,
  formatTime,
  formatMessage,
  submittingAction,
  onSubmit,
  errorBanner,
}: RsvpSurfaceProps): JSX.Element {
  // Map backend DTO `status` (covers Expired/Cancelled too) onto the union
  // accepted by GameNightDetailHero. Pending invitations to active game nights
  // are rendered as 'Published'; we already routed Cancelled/Expired to error
  // surfaces upstream so we only see live states here.
  const heroStatus: GameNightStatus = useMemo(() => {
    if (invitation.status === 'Cancelled') return 'Cancelled';
    if (invitation.status === 'Expired' || invitation.status === 'Declined') {
      return 'Completed';
    }
    return 'Published';
  }, [invitation.status]);

  const scheduledDate = useMemo(() => new Date(invitation.scheduledAt), [invitation.scheduledAt]);
  const scheduledLine = useMemo(() => {
    const dateLabel = formatDate(scheduledDate, {
      day: 'numeric',
      month: 'short',
      weekday: 'short',
    });
    const timeLabel = formatTime(scheduledDate, { hour: '2-digit', minute: '2-digit' });
    return `${dateLabel} · ${timeLabel}`;
  }, [formatDate, formatTime, scheduledDate]);

  const locationLine = invitation.location ?? t('pages.gameNightJoin.hero.locationMissing');

  const heroLabels: GameNightDetailHeroLabels = {
    statusLabel: '',
    scheduledLine,
    locationLine,
    organizedByLine: t('pages.gameNightJoin.hero.invitedBy', {
      hostName: invitation.hostDisplayName,
    }),
  };

  const currentResponse: RsvpAction | undefined = invitation.alreadyRespondedAs ?? undefined;

  const respondedByName = invitation.respondedByName ?? null;
  const alreadyRespondedBody =
    currentResponse === 'Accepted'
      ? respondedByName
        ? formatMessage(
            { id: 'pages.gameNightJoin.form.alreadyRespondedNamedAccepted' },
            { name: respondedByName }
          )
        : t('pages.gameNightJoin.form.alreadyRespondedAnonymousAccepted')
      : currentResponse === 'Declined'
        ? respondedByName
          ? formatMessage(
              { id: 'pages.gameNightJoin.form.alreadyRespondedNamedDeclined' },
              { name: respondedByName }
            )
          : t('pages.gameNightJoin.form.alreadyRespondedAnonymousDeclined')
        : '';

  const formLabels: PublicRsvpFormLabels = {
    sectionTitle: t('pages.gameNightJoin.form.sectionTitle'),
    displayNameLabel: t('pages.gameNightJoin.form.displayNameLabel'),
    displayNamePlaceholder: t('pages.gameNightJoin.form.displayNamePlaceholder'),
    displayNameHelper: t('pages.gameNightJoin.form.displayNameHelper'),
    displayNameTooLong: t('pages.gameNightJoin.form.displayNameTooLong'),
    accept: t('pages.gameNightJoin.form.accept'),
    decline: t('pages.gameNightJoin.form.decline'),
    submitting: t('pages.gameNightJoin.form.submitting'),
    alreadyRespondedHeading: t('pages.gameNightJoin.form.alreadyRespondedHeading'),
    alreadyRespondedBody,
    changeResponse: t('pages.gameNightJoin.form.changeResponse'),
  };

  return (
    <article
      data-slot="public-join-event-rsvp"
      className="flex flex-col gap-4 overflow-hidden rounded-2xl border border-border bg-card"
    >
      <GameNightDetailHero
        mode="public"
        title={invitation.title}
        status={heroStatus}
        labels={heroLabels}
        organizerId={invitation.hostUserId}
        organizerName={invitation.hostDisplayName}
      />

      {errorBanner ? (
        <div
          role="alert"
          aria-live="polite"
          data-slot="public-join-event-error-banner"
          className="mx-4 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {errorBanner}
        </div>
      ) : null}

      <div className="px-4 pb-4 md:px-6 md:pb-6">
        <PublicRsvpForm
          labels={formLabels}
          currentResponse={currentResponse}
          initialDisplayName={respondedByName}
          submittingAction={submittingAction}
          onSubmit={onSubmit}
        />
      </div>

      {/* Guest-activation CTA only after a successful response. */}
      {currentResponse !== undefined ? (
        <aside
          data-slot="public-join-event-create-account-cta"
          className="border-t border-border bg-muted px-4 py-4 md:px-6 md:py-5"
        >
          <h2 className="font-display text-sm font-extrabold text-foreground">
            {t('pages.gameNightJoin.cta.createAccount.title')}
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {t('pages.gameNightJoin.cta.createAccount.body')}
          </p>
          <Link
            href="/register"
            className="mt-3 inline-flex items-center justify-center rounded-md border-0 bg-primary px-3 py-2 font-display text-xs font-bold text-primary-foreground no-underline hover:bg-primary/90"
          >
            {t('pages.gameNightJoin.cta.createAccount.button')}
          </Link>
        </aside>
      ) : null}
    </article>
  );
}
