/**
 * FriendsActivitySection — priority #4 dashboard slot (Asse C, plan v2 WP5 T5).
 *
 * Renders a feed of recent friend activities (completed/created/joined) with
 * clickable avatars that open the player cascade drawer via the asse-B
 * cascade-store (`openDrawer('player', friendUserId)`).
 *
 * Empty state: shows EmptySection with "no recent activity" message.
 * Loading state: 3 skeleton entries.
 * Error state: hidden (silent fallback, returns null).
 * Default with empty array: returns null (silent fallback).
 *
 * Pure props-driven: data fetching (T7) and wiring into `DashboardClient`
 * (T8) are intentionally out of scope.
 */

'use client';

import type { JSX } from 'react';

import Link from 'next/link';

import type { FriendActivity, FriendActivityVerb } from '@/hooks/use-friends-activity';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';
import { SectionSkeleton } from './SectionSkeleton';

const VERB_LABEL_IT: Record<FriendActivityVerb, string> = {
  completed: 'ha completato',
  created: 'ha creato',
  joined: 'si è unito a',
};

const SECTION_ICON = '🎉';
// Issue #2178: previously 'Cosa fanno i tuoi' truncated mid-sentence.
const SECTION_TITLE = 'Cosa fanno i tuoi amici';
const SECTION_ID = 'friends-activity';

export type FriendsActivitySectionState = 'default' | 'empty' | 'loading' | 'error';

export interface FriendsActivitySectionProps {
  readonly state: FriendsActivitySectionState;
  readonly activities?: readonly FriendActivity[];
}

export function FriendsActivitySection({
  state,
  activities,
}: FriendsActivitySectionProps): JSX.Element | null {
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  // Error state — hidden silent.
  if (state === 'error') {
    return null;
  }

  // Empty state — entity-tinted "no activity" card.
  if (state === 'empty') {
    return (
      <DashboardSection
        sectionId={SECTION_ID}
        entity="player"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div data-testid="friends-empty">
          <EmptySection
            entity="player"
            icon="😴"
            message="Nessuna attività recente dai tuoi amici."
          />
        </div>
      </DashboardSection>
    );
  }

  // Loading state — 3 skeleton entries preserve list silhouette.
  if (state === 'loading') {
    return (
      <DashboardSection
        sectionId={SECTION_ID}
        entity="player"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div className="space-y-3" data-testid="friends-skeleton">
          <SectionSkeleton bodyMinHeight={48} />
          <SectionSkeleton bodyMinHeight={48} />
          <SectionSkeleton bodyMinHeight={48} />
        </div>
      </DashboardSection>
    );
  }

  // Default state.
  const items = activities ?? [];

  // Silent fallback when no items — treat as hidden (avoid empty visual noise).
  if (items.length === 0) {
    return null;
  }

  const handleAvatarClick = (friendUserId: string): void => {
    openDrawer('player', friendUserId);
  };

  return (
    <DashboardSection
      sectionId={SECTION_ID}
      entity="player"
      title={SECTION_TITLE}
      icon={SECTION_ICON}
      count={items.length}
    >
      <ul className="space-y-2" data-testid="friends-activity-list">
        {items.map((activity, idx) => (
          <li
            key={`${activity.friendUserId}-${activity.timestamp}-${idx}`}
            data-testid={`friends-entry-${activity.friendUserId}`}
            className="flex items-start gap-3 rounded-md border border-border p-3"
          >
            <button
              type="button"
              onClick={() => handleAvatarClick(activity.friendUserId)}
              data-testid={`friends-avatar-${activity.friendUserId}`}
              aria-label={`Apri profilo di ${activity.name}`}
              className="h-10 w-10 shrink-0 overflow-hidden rounded-full bg-muted"
            >
              {activity.avatar && (
                // eslint-disable-next-line @next/next/no-img-element -- avoid Next/Image domain config for external avatar URLs
                <img src={activity.avatar} alt="" className="h-full w-full object-cover" />
              )}
            </button>
            <div className="flex-1">
              <p className="text-sm">
                <span className="font-semibold">{activity.name}</span>{' '}
                <span className="text-muted-foreground">{VERB_LABEL_IT[activity.verb]}</span>{' '}
                {activity.gameOrEventType === 'game' ? (
                  <Link
                    href={`/games/${activity.gameOrEventId}`}
                    data-testid={`friends-ref-link-${activity.friendUserId}`}
                    className="font-medium hover:underline"
                  >
                    {activity.gameOrEventName}
                  </Link>
                ) : (
                  <Link
                    href={`/game-nights/${activity.gameOrEventId}`}
                    data-testid={`friends-ref-link-${activity.friendUserId}`}
                    className="font-medium hover:underline"
                  >
                    {activity.gameOrEventName}
                  </Link>
                )}
              </p>
              <time dateTime={activity.timestamp} className="text-xs text-muted-foreground">
                {formatRelativeTimestamp(activity.timestamp)}
              </time>
            </div>
          </li>
        ))}
      </ul>
    </DashboardSection>
  );
}

function formatRelativeTimestamp(iso: string): string {
  try {
    const date = new Date(iso);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'ora';
    if (diffMins < 60) return `${diffMins} min fa`;
    if (diffHours < 24) return `${diffHours}h fa`;
    if (diffDays === 1) return 'ieri';
    if (diffDays < 7) return `${diffDays}g fa`;
    return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
  } catch {
    return '—';
  }
}
