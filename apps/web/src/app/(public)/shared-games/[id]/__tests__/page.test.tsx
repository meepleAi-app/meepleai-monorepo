/**
 * Wave A.4 follow-up (Issue #616) — SSR page server-component tests.
 *
 * Verifies the contract from `page.tsx`:
 *  - Happy path: detail resolved → Suspense + page-client rendered with detail+contributors
 *  - 404: SharedGamesApiError(kind='http', httpStatus=404) → notFound() invoked
 *  - 5xx / network / timeout: rejection re-thrown so error.tsx boundary catches
 *  - Contributors fetch failure: non-fatal, page still renders with empty array
 *  - generateMetadata: builds title `${title} — MeepleAI`, description fallback,
 *    OpenGraph images conditional on imageUrl presence
 *  - generateMetadata swallows fetch errors (returns fallback "Gioco condiviso" title)
 *
 * Server Component tested via direct invocation:
 *   const page = await SharedGameDetailPage({ params: Promise.resolve({ id }) });
 *   render(page);
 *
 * `next/navigation` notFound() is mocked to throw a sentinel so we can assert
 * via expect(...).rejects.
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { SharedGameDetailV2, TopContributor } from '@/lib/api/shared-games';
import itMessages from '@/locales/it.json';

// Single source of truth for SSR metadata fallbacks (Wave A.4 / Issue #617).
// Tests assert against the same locale catalogue that `page.tsx` consumes
// in `generateMetadata`. If a future translator edits `it.json`, these
// assertions stay in sync automatically.
const META = itMessages.pages.sharedGameDetail.metadata;

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/navigation', () => ({
  notFound: vi.fn(() => {
    throw new Error('NEXT_NOT_FOUND');
  }),
}));

vi.mock('@/lib/shared-games/visual-test-fixture', () => ({
  tryLoadVisualTestFixture: vi.fn(() => null),
}));

vi.mock('@/lib/api/shared-games', async orig => {
  const actual = await orig<typeof import('@/lib/api/shared-games')>();
  return {
    ...actual,
    getSharedGameDetail: vi.fn(),
    getTopContributors: vi.fn(),
  };
});

vi.mock('../page-client', () => ({
  SharedGameDetailPageClient: ({
    id,
    detail,
    contributors,
  }: {
    id: string;
    detail: SharedGameDetailV2;
    contributors: readonly TopContributor[];
  }) => (
    <div
      data-testid="page-client-mock"
      data-id={id}
      data-title={detail.title}
      data-contributor-count={contributors.length}
    />
  ),
}));

// ============================================================================
// Imports (after mocks)
// ============================================================================

import { notFound } from 'next/navigation';

import {
  getSharedGameDetail,
  getTopContributors,
  SharedGamesApiError,
} from '@/lib/api/shared-games';

import SharedGameDetailPage, { generateMetadata } from '../page';

const mockGetDetail = getSharedGameDetail as ReturnType<typeof vi.fn>;
const mockGetContributors = getTopContributors as ReturnType<typeof vi.fn>;
const mockNotFound = notFound as ReturnType<typeof vi.fn>;

// ============================================================================
// Fixtures
// ============================================================================

const SAMPLE_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_DETAIL: SharedGameDetailV2 = {
  id: SAMPLE_ID,
  bggId: 13,
  title: 'Catan',
  yearPublished: 1995,
  description: 'Settlers strategy game',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 75,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.4,
  imageUrl: 'https://cdn.example.test/catan.png',
  thumbnailUrl: 'https://cdn.example.test/catan-thumb.png',
  status: 'Published',
  createdAt: '2026-04-15T00:00:00Z',
  modifiedAt: null,
  toolkits: [],
  agents: [],
  kbs: [],
  toolkitsCount: 0,
  agentsCount: 0,
  kbsCount: 0,
  contributorsCount: 0,
  hasKnowledgeBase: false,
  isTopRated: false,
  isNew: false,
};

const SAMPLE_CONTRIBUTORS: readonly TopContributor[] = [
  {
    userId: '22222222-2222-2222-2222-222222222222',
    displayName: 'Alice',
    avatarUrl: null,
    totalSessions: 5,
    totalWins: 3,
    score: 15,
  },
];

// ============================================================================
// Tests
// ============================================================================

describe('SharedGameDetailPage (Wave A.4 — Issue #616)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('default page() — SSR happy path', () => {
    it('renders the page-client when detail is resolved', async () => {
      mockGetDetail.mockResolvedValue(SAMPLE_DETAIL);
      mockGetContributors.mockResolvedValue(SAMPLE_CONTRIBUTORS);

      const page = await SharedGameDetailPage({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });
      render(page);

      const client = await screen.findByTestId('page-client-mock');
      expect(client).toBeInTheDocument();
      expect(client).toHaveAttribute('data-id', SAMPLE_ID);
      expect(client).toHaveAttribute('data-title', 'Catan');
      expect(client).toHaveAttribute('data-contributor-count', '1');
      expect(mockNotFound).not.toHaveBeenCalled();
    });

    it('passes empty contributors array when contributors fetch rejects (non-fatal)', async () => {
      mockGetDetail.mockResolvedValue(SAMPLE_DETAIL);
      mockGetContributors.mockRejectedValue(
        new SharedGamesApiError('boom', 'http', { httpStatus: 500 })
      );

      const page = await SharedGameDetailPage({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });
      render(page);

      const client = await screen.findByTestId('page-client-mock');
      expect(client).toHaveAttribute('data-contributor-count', '0');
    });
  });

  describe('default page() — error paths', () => {
    it('invokes notFound() on SharedGamesApiError httpStatus=404', async () => {
      mockGetDetail.mockRejectedValue(
        new SharedGamesApiError('Not found', 'http', { httpStatus: 404 })
      );
      mockGetContributors.mockResolvedValue([]);

      await expect(
        SharedGameDetailPage({ params: Promise.resolve({ id: SAMPLE_ID }) })
      ).rejects.toThrow('NEXT_NOT_FOUND');

      expect(mockNotFound).toHaveBeenCalledTimes(1);
    });

    it('re-throws non-404 SharedGamesApiError (5xx) so error.tsx boundary catches it', async () => {
      mockGetDetail.mockRejectedValue(
        new SharedGamesApiError('Server error', 'http', { httpStatus: 500 })
      );
      mockGetContributors.mockResolvedValue([]);

      await expect(
        SharedGameDetailPage({ params: Promise.resolve({ id: SAMPLE_ID }) })
      ).rejects.toThrow('Server error');

      expect(mockNotFound).not.toHaveBeenCalled();
    });

    it('re-throws on network/timeout SharedGamesApiError', async () => {
      mockGetDetail.mockRejectedValue(new SharedGamesApiError('Timeout', 'timeout'));
      mockGetContributors.mockResolvedValue([]);

      await expect(
        SharedGameDetailPage({ params: Promise.resolve({ id: SAMPLE_ID }) })
      ).rejects.toThrow('Timeout');

      expect(mockNotFound).not.toHaveBeenCalled();
    });

    it('re-throws non-Error rejection wrapped in Error', async () => {
      mockGetDetail.mockRejectedValue('string-rejection');
      mockGetContributors.mockResolvedValue([]);

      await expect(
        SharedGameDetailPage({ params: Promise.resolve({ id: SAMPLE_ID }) })
      ).rejects.toThrow('Unknown SSR failure loading shared game detail');

      expect(mockNotFound).not.toHaveBeenCalled();
    });
  });

  describe('generateMetadata()', () => {
    it('produces title "<title> — MeepleAI" and OG image when detail has imageUrl', async () => {
      mockGetDetail.mockResolvedValue(SAMPLE_DETAIL);

      const meta = await generateMetadata({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });

      expect(meta.title).toBe(`Catan${META.titleSuffix}`);
      expect(meta.description).toBe('Settlers strategy game');
      expect(meta.robots).toEqual({ index: true, follow: true });
      expect(meta.openGraph).toMatchObject({
        title: `Catan${META.titleSuffix}`,
        description: 'Settlers strategy game',
        type: 'website',
      });
      expect(meta.openGraph?.images).toEqual([{ url: SAMPLE_DETAIL.imageUrl }]);
    });

    it('truncates description to 200 chars', async () => {
      const longDescription = 'x'.repeat(300);
      mockGetDetail.mockResolvedValue({ ...SAMPLE_DETAIL, description: longDescription });

      const meta = await generateMetadata({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });

      expect(meta.description).toHaveLength(200);
      expect(meta.description).toBe('x'.repeat(200));
    });

    it('uses fallback description when detail.description is empty', async () => {
      mockGetDetail.mockResolvedValue({ ...SAMPLE_DETAIL, description: '' });

      const meta = await generateMetadata({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });

      expect(meta.description).toBe(META.descriptionFallback);
    });

    it('omits OG images when detail.imageUrl is empty string', async () => {
      mockGetDetail.mockResolvedValue({ ...SAMPLE_DETAIL, imageUrl: '' });

      const meta = await generateMetadata({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });

      expect(meta.openGraph?.images).toBeUndefined();
    });

    it('falls back to "Gioco condiviso" title when fetch rejects', async () => {
      mockGetDetail.mockRejectedValue(
        new SharedGamesApiError('Not found', 'http', { httpStatus: 404 })
      );

      const meta = await generateMetadata({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });

      expect(meta.title).toBe(`${META.titleFallback}${META.titleSuffix}`);
      expect(meta.description).toBe(META.descriptionFallback);
      expect(meta.openGraph?.images).toBeUndefined();
    });

    it('does not throw if fetch rejects with non-Error', async () => {
      mockGetDetail.mockRejectedValue('weird-rejection');

      const meta = await generateMetadata({
        params: Promise.resolve({ id: SAMPLE_ID }),
      });

      expect(meta.title).toBe(`${META.titleFallback}${META.titleSuffix}`);
    });
  });
});
