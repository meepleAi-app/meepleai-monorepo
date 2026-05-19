/**
 * RSVPCardLivePreview — desktop split-form live preview.
 * Issue #950 W3 Components. Spec §5 (C6).
 *
 * Renders the invitation card as the recipient will see it; subscribes to
 * the full WizardState so any change is reflected immediately.
 */

'use client';

import type { ReactElement } from 'react';

import type { LocationKind, WizardState } from '@/lib/game-nights/wizard-types';

export interface RSVPCardLivePreviewLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly rsvpAccept: string;
  readonly rsvpDecline: string;
  readonly noDate: string;
  readonly noLocation: string;
  readonly gamesTbd: string;
  readonly gamesNone: string;
  // PR #1297 review fix: section headers + location-fallback strings
  // must flow through the labels contract so non-IT locales don't leak
  // Italian into the preview card.
  readonly sectionWhen: string;
  readonly sectionWhere: string;
  readonly sectionWhat: string;
  readonly sectionWho: string;
  readonly kindHome: string;
  readonly kindFriend: string;
  readonly kindOnline: string;
}

export interface RSVPCardLivePreviewProps {
  readonly state: WizardState;
  readonly title: string;
  readonly organizerName: string;
  readonly games?: readonly { id: string; title: string }[];
  readonly labels: RSVPCardLivePreviewLabels;
}

function locationLine(
  kind: LocationKind,
  details: string,
  labels: RSVPCardLivePreviewLabels
): string {
  if (kind === 'tbd') return labels.noLocation;
  if (details.trim().length > 0) return details;
  // PR #1297 review fix: each kind routes through labels (was hardcoded IT;
  // the `home` branch also produced the literal string "Casa di home" due to
  // an incorrect concat from an unfinished placeholder).
  switch (kind) {
    case 'home':
      return labels.kindHome;
    case 'friend':
      return labels.kindFriend;
    case 'online':
      return labels.kindOnline;
  }
}

function gamesLine(
  state: WizardState,
  games: readonly { id: string; title: string }[],
  labels: RSVPCardLivePreviewLabels
): string {
  if (state.games.decideAtGroup) return labels.gamesTbd;
  if (state.games.selected.length === 0) return labels.gamesNone;
  const titles = state.games.selected
    .map(id => games.find(g => g.id === id)?.title ?? id)
    .join(' · ');
  return titles;
}

function formatDate(iso: string | null, fallback: string): string {
  if (!iso) return fallback;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return fallback;
  return d.toLocaleString(undefined, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function RSVPCardLivePreview({
  state,
  title,
  organizerName,
  games = [],
  labels,
}: RSVPCardLivePreviewProps): ReactElement {
  return (
    <aside
      data-slot="game-night-create-preview"
      aria-label={labels.title}
      className="flex flex-col gap-4 rounded-lg border border-border bg-card p-4"
    >
      <header>
        <p className="text-xs uppercase tracking-wider text-muted-foreground">{labels.title}</p>
        <p className="text-xs text-muted-foreground">{labels.subtitle}</p>
      </header>

      <div className="flex flex-col gap-1">
        <p className="text-base font-semibold text-foreground">{title || '—'}</p>
        <p className="text-xs text-muted-foreground">{organizerName}</p>
      </div>

      <dl className="flex flex-col gap-2 text-sm">
        <div>
          <dt className="text-xs uppercase text-muted-foreground">{labels.sectionWhen}</dt>
          <dd className="text-foreground">{formatDate(state.date.iso, labels.noDate)}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-muted-foreground">{labels.sectionWhere}</dt>
          <dd className="text-foreground">
            {locationLine(state.location.kind, state.location.details, labels)}
          </dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-muted-foreground">{labels.sectionWhat}</dt>
          <dd className="text-foreground">{gamesLine(state, games, labels)}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-muted-foreground">{labels.sectionWho}</dt>
          <dd className="text-foreground">
            {state.invitees.length === 0
              ? '—'
              : state.invitees.map(i => (i.kind === 'user' ? i.displayName : i.address)).join(', ')}
          </dd>
        </div>
      </dl>

      <footer className="flex gap-2 pt-2">
        <button
          type="button"
          disabled
          className="flex-1 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground opacity-60"
        >
          {labels.rsvpAccept}
        </button>
        <button
          type="button"
          disabled
          className="flex-1 rounded-md border border-border px-3 py-2 text-sm text-muted-foreground opacity-60"
        >
          {labels.rsvpDecline}
        </button>
      </footer>
    </aside>
  );
}
