/**
 * MeepleCardGame — community shared-game tile for /shared-games index.
 *
 * Issue #2858 (C1): thin adapter over the canonical MeepleCard
 * (entity="game", variant="grid"). Previously a standalone renderer that
 * re-implemented cover/stars/badge inline; now composes MeepleCard and maps
 * the shared-games signals to canonical props. The public prop interface is
 * unchanged except `compact` (an unwired responsive knob) which was removed.
 *
 * Navigation: passes `href` so GridCard renders a real `<Link prefetch>` root
 * (public route needs prefetch + open-in-new-tab + native focus). The Wikidata
 * attribution footer is emitted by MeepleCard (entity=game) as a sibling of the
 * card root — no nested anchor.
 *
 * `labels.ratingAriaLabel` / `labels.newWeekAriaLabel` are retained on the
 * interface (to avoid churning /shared-games page-client) but are no longer
 * consumed by the canonical render; a follow-up may prune them.
 */
import type { JSX, ReactNode } from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { ConnectionChipProps } from '@/components/ui/data-display/meeple-card/types';

export interface MeepleCardGameLabels {
  /** Aria label prefix for the rating (retained for interface stability). */
  readonly ratingAriaLabel: string;
  /** Footer chip label for the toolkit count. */
  readonly toolkitLabel: string;
  /** Footer chip label for the agent count. */
  readonly agentLabel: string;
  /** Aria label fragment for the newWeek badge (retained for interface stability). */
  readonly newWeekAriaLabel: (count: number) => string;
}

export interface MeepleCardGameProps {
  readonly id: string;
  readonly title: string;
  /** Optional cover image; falls back to a tinted 🎲 emoji placeholder when absent. */
  readonly coverUrl?: string | null;
  /** Year published (rendered as the subtitle). */
  readonly year?: number | null;
  /** Average rating in 0..5 scale (already converted from backend 0..10). */
  readonly rating: number;
  readonly toolkitsCount: number;
  readonly agentsCount: number;
  readonly kbsCount: number;
  /** Count of children created this week (>=2 triggers the visible badge). */
  readonly newThisWeekCount: number;
  readonly labels: MeepleCardGameLabels;
  readonly className?: string;
  /**
   * Issue #2055 Phase 7 — Wikidata cover attribution fields. Forwarded to
   * MeepleCard, which renders MeepleCardAttributionFooter for entity=game.
   */
  readonly coverLicense?: string | null;
  readonly coverAttribution?: string | null;
  readonly coverSourceUrl?: string | null;
  /**
   * Issue #3470 Slice 1d-c — optional admin cover-edit affordance forwarded to the
   * canonical MeepleCard cover slot (rendered outside the card anchor). Omitted for
   * non-admins so the tile renders unchanged.
   */
  readonly coverEditSlot?: ReactNode;
}

export function MeepleCardGame({
  id,
  title,
  coverUrl,
  year,
  rating,
  toolkitsCount,
  agentsCount,
  kbsCount,
  newThisWeekCount,
  labels,
  className,
  coverLicense = null,
  coverAttribution = null,
  coverSourceUrl = null,
  coverEditSlot,
}: MeepleCardGameProps): JSX.Element {
  const connections: ConnectionChipProps[] = [];
  if (toolkitsCount > 0) {
    connections.push({
      entityType: 'toolkit',
      count: toolkitsCount,
      label: labels.toolkitLabel,
      showLabel: true,
    });
  }
  if (agentsCount > 0) {
    connections.push({
      entityType: 'agent',
      count: agentsCount,
      label: labels.agentLabel,
      showLabel: true,
    });
  }
  if (kbsCount > 0) {
    connections.push({ entityType: 'kb', count: kbsCount, showLabel: false });
  }

  const badge = newThisWeekCount >= 2 ? `+${newThisWeekCount}` : undefined;

  return (
    <MeepleCard
      entity="game"
      variant="grid"
      href={`/shared-games/${id}`}
      title={title}
      subtitle={year != null ? String(year) : undefined}
      imageUrl={coverUrl ?? undefined}
      coverEmoji="🎲"
      rating={rating}
      ratingMax={5}
      badge={badge}
      connections={connections}
      className={className}
      data-testid="shared-games-card"
      coverLicense={coverLicense}
      coverAttribution={coverAttribution}
      coverSourceUrl={coverSourceUrl}
      coverEditSlot={coverEditSlot}
    />
  );
}
