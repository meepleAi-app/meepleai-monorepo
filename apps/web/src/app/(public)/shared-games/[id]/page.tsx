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
  SharedGamesApiError,
  type SharedGameDetailV2,
  type TopContributor,
} from '@/lib/api/shared-games';
import { tryLoadVisualTestFixture } from '@/lib/shared-games/visual-test-fixture';
import itMessages from '@/locales/it.json';

import { SharedGameDetailPageClient } from './page-client';

import type { Metadata } from 'next';

export const revalidate = 60;

/**
 * SSR-safe i18n metadata source. The `IntlProvider` only runs in the
 * client tree, so `generateMetadata` cannot use `useTranslations`.
 * We import the IT catalogue (project default — see `locales/index.ts`
 * `DEFAULT_LOCALE = LOCALES.IT`) directly. Wave A.4 follow-up — Issue #617.
 */
const SSR_METADATA = itMessages.pages.sharedGameDetail.metadata;

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
  // Visual-regression bootstrap escape hatch (Wave A.4, Issue #603).
  // Active only when the build was produced with
  // `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` (set by
  // `.github/workflows/visual-regression-migrated.yml`). In production
  // deploys the constant evaluates to `false` and this branch is
  // dead-code-eliminated by the bundler.
  const fixture = tryLoadVisualTestFixture(id);
  if (fixture) {
    return { detail: fixture.detail, contributors: fixture.contributors };
  }

  let detail: SharedGameDetailV2 | null = null;
  let contributors: readonly TopContributor[] = [];

  // SSR timeout cap: the public page must not hang the Next.js render
  // when the backend is degraded. 2000ms aligns with the SLO p95 target
  // (200ms) plus a 10x safety margin — beyond that we fail open with
  // the visual fallback states. Issue #615.
  const [detailResult, contributorsResult] = await Promise.allSettled([
    getSharedGameDetail(id, { next: { revalidate: 60 }, timeoutMs: 2000 }),
    getTopContributors(8, { next: { revalidate: 60 }, timeoutMs: 2000 }),
  ]);

  if (detailResult.status === 'fulfilled') {
    detail = detailResult.value;
  } else {
    // Distinguish 404 (legitimate "no such game" → notFound()) from
    // 5xx / network / timeout / parse failures (→ error.tsx). Re-throwing
    // here lets Next.js bubble the error up to the nearest error boundary.
    // Issue #615.
    const reason = detailResult.reason;
    const isHttp404 =
      reason instanceof SharedGamesApiError && reason.kind === 'http' && reason.httpStatus === 404;
    if (!isHttp404) {
      throw reason instanceof Error
        ? reason
        : new Error('Unknown SSR failure loading shared game detail');
    }
    // 404: fall through with detail = null; page() calls notFound().
  }

  if (contributorsResult.status === 'fulfilled') {
    contributors = contributorsResult.value;
  }
  // Contributors failure is non-fatal — the carousel just renders empty.

  return { detail, contributors };
}

export async function generateMetadata({ params }: SharedGameDetailPageProps): Promise<Metadata> {
  const { id } = await params;

  // Same fixture short-circuit as `loadInitialData` — keeps SEO metadata
  // in sync during visual-regression bootstrap (and dead in production).
  const fixture = tryLoadVisualTestFixture(id);
  let detail: SharedGameDetailV2 | null = fixture?.detail ?? null;
  if (!detail) {
    try {
      detail = await getSharedGameDetail(id, {
        next: { revalidate: 60 },
        timeoutMs: 2000,
      });
    } catch {
      // Fall through: page will render notFound() if detail is null.
    }
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
    robots: { index: true, follow: true },
    openGraph: {
      title: fullTitle,
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
