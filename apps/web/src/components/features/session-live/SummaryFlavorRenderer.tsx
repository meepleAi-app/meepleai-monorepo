'use client';

import type { ComponentType } from 'react';

import dynamic from 'next/dynamic';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

import { FlavorLoadingSkeleton } from './FlavorLoadingSkeleton';

export interface SummaryFlavorProps {
  readonly session: GameSessionDto;
  readonly className?: string;
}

type SummaryFlavorComponent = ComponentType<SummaryFlavorProps>;

// Lazy chunks minted at MODULE scope (never inside render) — same rule as FlavorRenderer.
// This is the SUMMARY twin of FlavorRenderer: props are keyed on GameSessionDto (not the
// live LiveSessionDto), so the two dispatchers stay type-disjoint.
const CatanSummaryFlavorLazy: SummaryFlavorComponent = dynamic(
  () => import('./flavors/catan/CatanSummaryFlavor').then(m => ({ default: m.CatanSummaryFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);

const SUMMARY_FLAVOR_MAP: Record<string, SummaryFlavorComponent> = {
  catan: CatanSummaryFlavorLazy,
};

export function hasSummaryFlavor(gameSlug: string | null | undefined): boolean {
  return gameSlug != null && SUMMARY_FLAVOR_MAP[gameSlug] != null;
}

interface SummaryFlavorRendererProps extends SummaryFlavorProps {
  readonly gameSlug: string | null | undefined;
}

export function SummaryFlavorRenderer({
  gameSlug,
  session,
  className,
}: SummaryFlavorRendererProps): React.JSX.Element | null {
  const LazyFlavor = gameSlug != null ? SUMMARY_FLAVOR_MAP[gameSlug] : undefined;
  if (LazyFlavor == null) return null;
  return <LazyFlavor session={session} className={className} />;
}
