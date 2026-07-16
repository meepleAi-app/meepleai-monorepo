'use client';

import { type ComponentType, type ReactElement } from 'react';

import dynamic from 'next/dynamic';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import type { ParticipantRole } from '@/lib/session-live/participant-role';

import { FlavorLoadingSkeleton } from './FlavorLoadingSkeleton';

export type FlavorView = 'live';

/** Game-agnostic props every per-game flavor component must accept. */
export interface FlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  /** #2787: live SignalR points (playerId→points) forwarded to the flavor. */
  readonly livePoints?: ReadonlyMap<string, number> | null;
  /** #2787: current phase name forwarded to the flavor. */
  readonly phaseName?: string | null;
}

type FlavorComponent = ComponentType<FlavorProps>;

// Lazy chunks are created at MODULE scope — NEVER inside render (that would
// mint a new component identity every render → remount loop). The loader
// returns `{ default }` to match the codebase precedent (editor/page.tsx,
// KbGlobaleView.tsx) and satisfy next/dynamic's TS loader type.
const CatanLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/catan/CatanLiveFlavor').then(m => ({ default: m.CatanLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);

const WingspanLiveFlavorLazy: FlavorComponent = dynamic(
  () =>
    import('./flavors/wingspan/WingspanLiveFlavor').then(m => ({
      default: m.WingspanLiveFlavor,
    })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);

/**
 * ADR-070 Option B — per-game flavor registry. Each value is a module-level
 * lazy component (content-hashed chunk fetched ONLY when that game's live
 * session is opened; verified by pnpm bundle:check). Summary entries arrive
 * with G6a-2; other 6 games with G6b–g.
 */
const FLAVOR_MAP: Record<string, Partial<Record<FlavorView, FlavorComponent>>> = {
  catan: { live: CatanLiveFlavorLazy },
  wingspan: { live: WingspanLiveFlavorLazy },
};

export function hasFlavor(gameSlug: string | null | undefined): boolean {
  return gameSlug != null && FLAVOR_MAP[gameSlug]?.live != null;
}

export interface FlavorRendererProps extends FlavorProps {
  readonly gameSlug: string | null | undefined;
  readonly view: FlavorView;
}

export function FlavorRenderer({
  gameSlug,
  view,
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
  phaseName,
}: FlavorRendererProps): ReactElement | null {
  const LazyFlavor = gameSlug != null ? FLAVOR_MAP[gameSlug]?.[view] : undefined;
  if (LazyFlavor == null) return null;
  return (
    <LazyFlavor
      session={session}
      viewerRole={viewerRole}
      sessionId={sessionId}
      className={className}
      livePoints={livePoints}
      phaseName={phaseName}
    />
  );
}
