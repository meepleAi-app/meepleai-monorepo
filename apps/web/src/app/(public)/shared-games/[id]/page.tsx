/**
 * /shared-games/[id] — public detail page (V2, Wave A.4, Issue #603).
 *
 * Server component shell:
 *   - ISR `revalidate = 60` (aligned with backend HybridCache TTL — Wave A.3a §3.6)
 *   - Dynamic SEO metadata via `generateMetadata({ params })`
 *   - SSR seed via 2-way `Promise.allSettled` (detail + global top contributors)
 *   - 404 via `notFound()` when detail fetch fails (typically 404 from backend)
 *
 * Client orchestration delegated to `./page-client.tsx`.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-shared-game-detail.jsx`
 * Spec: `docs/superpowers/specs/2026-04-28-v2-migration-wave-a-4-shared-game-detail.md`
 */

import { Suspense, type JSX } from 'react';

import { notFound } from 'next/navigation';

import {
  getSharedGameDetail,
  getTopContributors,
  type SharedGameDetailV2,
  type TopContributor,
} from '@/lib/api/shared-games';

import { SharedGameDetailPageClient } from './page-client';

import type { Metadata } from 'next';

export const revalidate = 60;

interface SharedGameDetailRouteParams {
  readonly id: string;
}

interface SharedGameDetailPageProps {
  readonly params: Promise<SharedGameDetailRouteParams>;
}

interface SsrInitialData {
  readonly detail: SharedGameDetailV2 | null;
  readonly contributors: readonly TopContributor[];
}

async function loadInitialData(id: string): Promise<SsrInitialData> {
  let detail: SharedGameDetailV2 | null = null;
  let contributors: readonly TopContributor[] = [];

  try {
    const [detailResult, contributorsResult] = await Promise.allSettled([
      getSharedGameDetail(id, { next: { revalidate: 60 } }),
      getTopContributors(8, { next: { revalidate: 60 } }),
    ]);

    if (detailResult.status === 'fulfilled') {
      detail = detailResult.value;
    }
    if (contributorsResult.status === 'fulfilled') {
      contributors = contributorsResult.value;
    }
  } catch {
    // Defense in depth — Promise.allSettled doesn't reject. Fall through with defaults.
  }

  return { detail, contributors };
}

export async function generateMetadata({ params }: SharedGameDetailPageProps): Promise<Metadata> {
  const { id } = await params;

  let detail: SharedGameDetailV2 | null = null;
  try {
    detail = await getSharedGameDetail(id, { next: { revalidate: 60 } });
  } catch {
    // Fall through: page will render notFound() if detail is null.
  }

  const baseTitle = detail?.title ?? 'Gioco condiviso';
  const description =
    detail?.description && detail.description.length > 0
      ? detail.description.slice(0, 200)
      : 'Dettagli del gioco condiviso dalla community: toolkit, agenti AI e knowledge base.';

  return {
    title: `${baseTitle} — MeepleAI`,
    description,
    robots: { index: true, follow: true },
    openGraph: {
      title: `${baseTitle} — MeepleAI`,
      description,
      type: 'website',
      images:
        detail?.imageUrl && detail.imageUrl.length > 0 ? [{ url: detail.imageUrl }] : undefined,
    },
  };
}

export default async function SharedGameDetailPage({
  params,
}: SharedGameDetailPageProps): Promise<JSX.Element> {
  const { id } = await params;
  const { detail, contributors } = await loadInitialData(id);

  if (!detail) {
    notFound();
  }

  return (
    <Suspense fallback={<SharedGameDetailFallback />}>
      <SharedGameDetailPageClient id={id} detail={detail} contributors={contributors} />
    </Suspense>
  );
}

function SharedGameDetailFallback(): JSX.Element {
  return (
    <main aria-busy="true" aria-live="polite" className="min-h-screen bg-background">
      <div className="mx-auto max-w-[1280px] px-4 py-12 sm:px-8" />
    </main>
  );
}
