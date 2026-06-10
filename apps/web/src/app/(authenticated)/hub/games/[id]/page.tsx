/**
 * /hub/games/[id] — authenticated detail page (#2118).
 *
 * Mirrors the public catalog detail at `/shared-games/[id]` (#603), but renders
 * inside the `(authenticated)` `UserShell` so the `AppTopBar` stays mounted
 * during intra-`/hub/*` navigation. The previous implementation was a thin
 * `redirect('/shared-games/[id]')` which re-introduced the chrome drift this
 * PR is supposed to fix.
 *
 * Server-component shell:
 *   - ISR `revalidate = 60` (aligned with HybridCache TTL — Wave A.3a §3.6)
 *   - `Promise.allSettled` 2-way fetch (detail + global top contributors)
 *   - `notFound()` on backend 404
 *   - OG metadata; `robots: noindex` — SEO is owned by the public mirror
 *
 * Render delegate is `SharedGameDetailPageClient` from the `(public)` sibling,
 * reused 1:1. Loader logic is intentionally duplicated from the public route
 * shell — the cleaner consolidation (`lib/shared-games/detail-loader.ts`) is
 * tracked as a follow-up and out of scope for this PR.
 *
 * Skipped vs. public mirror:
 *   - `tryLoadVisualTestFixture` (visual-regression bootstrap is anon-only)
 */

import { Suspense, type JSX } from 'react';

import { notFound } from 'next/navigation';

import { SharedGameDetailPageClient } from '@/app/(public)/shared-games/[id]/page-client';
import {
  getSharedGameDetail,
  getTopContributors,
  SharedGamesApiError,
  type SharedGameDetail,
  type TopContributor,
} from '@/lib/api/shared-games';
import { getSsrMessages } from '@/lib/i18n/ssr';

import type { Metadata } from 'next';

export const revalidate = 60;

const SSR_METADATA = getSsrMessages('pages.sharedGameDetail.metadata');

interface RouteParams {
  readonly id: string;
}

interface PageProps {
  readonly params: Promise<RouteParams>;
}

interface SsrInitialData {
  readonly detail: SharedGameDetail | null;
  readonly contributors: readonly TopContributor[];
}

async function loadInitialData(id: string): Promise<SsrInitialData> {
  let detail: SharedGameDetail | null = null;
  let contributors: readonly TopContributor[] = [];

  const [detailResult, contributorsResult] = await Promise.allSettled([
    getSharedGameDetail(id, { next: { revalidate: 60 }, timeoutMs: 2000 }),
    getTopContributors(8, { next: { revalidate: 60 }, timeoutMs: 2000 }),
  ]);

  if (detailResult.status === 'fulfilled') {
    detail = detailResult.value;
  } else {
    const reason = detailResult.reason;
    const isHttp404 =
      reason instanceof SharedGamesApiError && reason.kind === 'http' && reason.httpStatus === 404;
    if (!isHttp404) {
      throw reason instanceof Error
        ? reason
        : new Error('Unknown SSR failure loading shared game detail');
    }
    // 404 → fall through with detail = null; page() calls notFound().
  }

  if (contributorsResult.status === 'fulfilled') {
    contributors = contributorsResult.value;
  }
  // Contributors failure is non-fatal — carousel renders empty.

  return { detail, contributors };
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { id } = await params;

  let detail: SharedGameDetail | null = null;
  try {
    detail = await getSharedGameDetail(id, {
      next: { revalidate: 60 },
      timeoutMs: 2000,
    });
  } catch {
    // Fall through — page will render notFound() if detail is null.
  }

  const baseTitle = detail?.title ?? SSR_METADATA.titleFallback;
  const fullTitle = `${baseTitle}${SSR_METADATA.titleSuffix}`;
  const description =
    detail?.description && detail.description.length > 0
      ? detail.description.slice(0, 200)
      : SSR_METADATA.descriptionFallback;

  return {
    title: fullTitle,
    description,
    // Auth-gated detail: the public mirror at /shared-games/[id] owns SEO.
    robots: { index: false, follow: false },
    openGraph: {
      title: fullTitle,
      description,
      type: 'website',
      images:
        detail?.imageUrl && detail.imageUrl.length > 0 ? [{ url: detail.imageUrl }] : undefined,
    },
  };
}

export default async function HubGameDetailPage({ params }: PageProps): Promise<JSX.Element> {
  const { id } = await params;
  const { detail, contributors } = await loadInitialData(id);

  if (!detail) {
    notFound();
  }

  return (
    <Suspense fallback={<HubGameDetailFallback />}>
      <SharedGameDetailPageClient
        id={id}
        detail={detail}
        contributors={contributors}
        hideStickyCta
      />
    </Suspense>
  );
}

function HubGameDetailFallback(): JSX.Element {
  return (
    <main aria-busy="true" aria-live="polite" className="min-h-screen bg-background">
      <div className="mx-auto max-w-[1280px] px-4 py-12 sm:px-8" />
    </main>
  );
}
