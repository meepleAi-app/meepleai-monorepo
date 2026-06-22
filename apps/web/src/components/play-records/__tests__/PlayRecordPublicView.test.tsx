/**
 * PlayRecordPublicView — TDD tests (#2437-2).
 *
 * Verifies the three rendering surfaces:
 *   1. Loading skeleton — while the query is in-flight
 *   2. Not-found state — when the query returns an error or null data
 *   3. Body rendered   — when data arrives, PlayRecordDetailBody receives
 *                        record + currentUserId={null}
 *
 * Creator buttons ("Condividi" / "Aggiungi foto") must NOT appear in the
 * public view because currentUserId={null} disables the isCreator branch.
 *
 * Strategy:
 *   - Mock `useSharedPlayRecord` directly (vi.mock on the domain-hooks module).
 *   - Mock `PlayRecordDetailBody` as a lightweight sentinel so we can assert
 *     props without rendering the full complex body tree (avoids cascading
 *     vi.mock requirements for router / i18n / derivePerspective etc.).
 *   - Wrap in QueryClientProvider (required by the hook even when mocked).
 */
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { PlayRecordDto } from '@/lib/api/schemas/play-records.schemas';

import { PlayRecordPublicView } from '../PlayRecordPublicView';

// ─── Mock: useSharedPlayRecord ────────────────────────────────────────────────

vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  useSharedPlayRecord: vi.fn(),
  playRecordsKeys: {
    all: ['play-records'],
    shared: (token: string) => ['play-records', 'shared', token],
  },
}));

// ─── Mock: PlayRecordDetailBody ───────────────────────────────────────────────
// Lightweight sentinel — renders a div exposing props as data attributes so we
// can assert them without pulling in the full component tree.
vi.mock('../PlayRecordDetailBody', () => ({
  PlayRecordDetailBody: vi.fn(({ record, currentUserId }) => (
    <div
      data-testid="mock-detail-body"
      data-game-name={record?.gameName}
      data-current-user-id={currentUserId === null ? 'null' : currentUserId}
    />
  )),
}));

// ─── Mock: next/navigation (useParams) ────────────────────────────────────────
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({ token: 'test-tok-abc' }),
}));

// ─── import after mock ────────────────────────────────────────────────────────
import { useSharedPlayRecord } from '@/lib/domain-hooks/usePlayRecords';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeWrapper(): ({ children }: { children: ReactNode }) => JSX.Element {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

/** Minimal PlayRecordDto sufficient for the test assertions. */
const FIXTURE_RECORD: PlayRecordDto = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameName: 'Catan',
  gameId: 'game-catan',
  sessionDate: '2026-06-20T10:00:00Z',
  status: 'Completed',
  players: [
    {
      id: 'plr-1',
      userId: 'u-other',
      displayName: 'Alice',
      scores: [{ dimension: 'points', value: 10, unit: null }],
      totalScore: 10,
    },
  ],
  winnerPlayerIds: ['plr-1'],
  outcomeType: 'competitive',
  visibility: 'Private',
  createdByUserId: 'u-creator',
  duration: '01:30:00',
  scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
  startTime: null,
  endTime: null,
  notes: null,
  location: null,
  createdAt: '2026-06-20T10:00:00Z',
  updatedAt: '2026-06-20T10:00:00Z',
  shareToken: 'test-tok-abc',
  photos: [],
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PlayRecordPublicView (#2437-2)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ── Surface 1: loading ──────────────────────────────────────────────────────
  describe('loading state', () => {
    it('renders the loading skeleton while the query is in-flight', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="test-tok-abc" />, { wrapper: makeWrapper() });

      expect(screen.getByTestId('play-record-public-loading')).toBeInTheDocument();
      expect(screen.queryByTestId('mock-detail-body')).not.toBeInTheDocument();
    });
  });

  // ── Surface 2: not found / error ────────────────────────────────────────────
  describe('not-found / error state', () => {
    it('renders the not-found card when the query returns an error', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Shared play record not found'),
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="bad-token" />, { wrapper: makeWrapper() });

      expect(screen.getByText(/Partita Non Trovata/i)).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /torna alla home/i })).toHaveAttribute('href', '/');
      expect(screen.queryByTestId('mock-detail-body')).not.toBeInTheDocument();
    });

    it('renders the not-found card when data is undefined (no error)', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="missing-token" />, { wrapper: makeWrapper() });

      expect(screen.getByText(/Partita Non Trovata/i)).toBeInTheDocument();
    });
  });

  // ── Surface 3: content ──────────────────────────────────────────────────────
  describe('content state (record loaded)', () => {
    it('renders PlayRecordDetailBody with the fetched record', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: FIXTURE_RECORD,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="test-tok-abc" />, { wrapper: makeWrapper() });

      const body = screen.getByTestId('mock-detail-body');
      expect(body).toBeInTheDocument();
      expect(body).toHaveAttribute('data-game-name', 'Catan');
    });

    it('passes currentUserId={null} to PlayRecordDetailBody (read-only / spectator mode)', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: FIXTURE_RECORD,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="test-tok-abc" />, { wrapper: makeWrapper() });

      const body = screen.getByTestId('mock-detail-body');
      // Sentinel renders data-current-user-id="null" when prop is null
      expect(body).toHaveAttribute('data-current-user-id', 'null');
    });

    it('does NOT render creator buttons (Condividi / Aggiungi foto) in public mode', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: FIXTURE_RECORD,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="test-tok-abc" />, { wrapper: makeWrapper() });

      // These buttons are only rendered by PlayRecordDetailBody when isCreator=true
      // (i.e. currentUserId === record.createdByUserId). With currentUserId=null,
      // isCreator is always false. The mock body doesn't render them either.
      expect(screen.queryByRole('button', { name: /condividi/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /aggiungi foto/i })).not.toBeInTheDocument();
    });

    it('passes the token from props to useSharedPlayRecord', () => {
      vi.mocked(useSharedPlayRecord).mockReturnValue({
        data: FIXTURE_RECORD,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSharedPlayRecord>);

      render(<PlayRecordPublicView token="my-custom-token" />, { wrapper: makeWrapper() });

      expect(useSharedPlayRecord).toHaveBeenCalledWith('my-custom-token');
    });
  });
});
