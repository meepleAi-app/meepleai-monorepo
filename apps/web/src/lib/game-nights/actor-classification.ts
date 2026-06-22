/**
 * Actor classification for /game-nights/[id] (Issue #951, AC-H3).
 *
 * Classifies the current viewer into one of three actors with distinct
 * permissions and UI affordances:
 *
 * - Host: creator of the event. Can edit, cancel, kick guests.
 * - Guest: invited user with an RSVP entry. Can RSVP yes/no/maybe.
 * - Bystander: authenticated user with neither role. Behaviour depends on
 *   the event's visibility setting (public read-only vs private gated).
 *
 * Why: implements §AC-H3 of issue #951 spec-hardening (Cockburn actor matrix).
 *      Used by the page-level guard in `app/(authenticated)/game-nights/[id]`
 *      and by hook composition in `useGameNightDetailActor`.
 */

import type { GameNightDto, GameNightRsvpDto } from '@/lib/api/schemas/game-nights.schemas';

export type GameNightActor = 'host' | 'guest' | 'bystander';

export interface GameNightActorContext {
  /** Discriminator for routing UI affordances. */
  actor: GameNightActor;
  /** When actor='guest', the viewer's RSVP entry (always present). */
  rsvp?: GameNightRsvpDto;
  /**
   * When actor='bystander', whether the event is publicly visible.
   * `false` → render "Serata privata" gated empty state with request-invite CTA.
   * `true`  → render read-only info + public RSVP CTA (future, gated by visibility flag).
   */
  isPublicEvent?: boolean;
}

/**
 * Visibility flag — currently always private in v1 schema. The bystander
 * branch keeps a placeholder for future public-event support without breaking
 * downstream consumers when the flag is added to GameNightDto.
 */
const DEFAULT_PUBLIC_VISIBILITY = false;

export interface ClassifyActorInput {
  /** ID of the authenticated viewer. Required — unauthenticated never reaches this page. */
  viewerId: string;
  /** Event aggregate. */
  event: GameNightDto;
  /** RSVP roster as returned by `useGameNightRsvps`. */
  rsvps: readonly GameNightRsvpDto[] | undefined;
}

export function classifyGameNightActor(input: ClassifyActorInput): GameNightActorContext {
  const { viewerId, event, rsvps } = input;

  if (event.organizerId === viewerId) {
    return { actor: 'host' };
  }

  const viewerRsvp = rsvps?.find(r => r.userId === viewerId);
  if (viewerRsvp) {
    return { actor: 'guest', rsvp: viewerRsvp };
  }

  return { actor: 'bystander', isPublicEvent: DEFAULT_PUBLIC_VISIBILITY };
}

/**
 * Permission predicates — UI surface boundary. Keep these single-purpose:
 * components ask "can I do X?" rather than reconstructing actor logic.
 */
export const can = {
  edit: (ctx: GameNightActorContext): boolean => ctx.actor === 'host',
  cancel: (ctx: GameNightActorContext): boolean => ctx.actor === 'host',
  kickGuest: (ctx: GameNightActorContext): boolean => ctx.actor === 'host',
  rsvp: (ctx: GameNightActorContext): boolean => ctx.actor === 'guest',
  requestInvite: (ctx: GameNightActorContext): boolean =>
    ctx.actor === 'bystander' && ctx.isPublicEvent === false,
  viewPrivateRoster: (ctx: GameNightActorContext): boolean =>
    ctx.actor === 'host' || ctx.actor === 'guest',
};
