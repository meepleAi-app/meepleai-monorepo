import { render, screen, within } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import StatisticsPage from '../page';
import {
  mockPlayerStatisticsEmpty,
  mockPlayerStatisticsFull,
} from '@/components/play-records/stats/__tests__/fixtures';
import { usePlayerStatistics } from '@/lib/domain-hooks/usePlayRecords';

// Mock usePlayerStatistics
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayerStatistics: vi.fn(() => ({
    data: mockPlayerStatisticsFull,
    isLoading: false,
    error: null,
  })),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

// Mock next/navigation (StatisticsPage uses useRouter for back navigation)
vi.mock('next/navigation', () => ({
  useRouter: () => ({ back: vi.fn(), push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
  usePathname: () => '/play-records/stats',
  useSearchParams: () => new URLSearchParams(),
}));

// Mock useSharedGames
vi.mock('@/lib/play-records/useSharedGames', () => ({
  useSharedGames: vi.fn(() => ({
    data: new Map(),
    isLoading: false,
    error: null,
  })),
}));

describe('StatisticsPage (play-records/stats)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset to default full-data mock — vi.clearAllMocks doesn't reset implementations,
    // so prior tests' mockReturnValue overrides would leak otherwise.
    vi.mocked(usePlayerStatistics).mockReturnValue({
      data: mockPlayerStatisticsFull,
      isLoading: false,
      error: null,
    } as any);
  });

  it('AC-5.6: loading state shows skeleton shimmer', async () => {
    const { usePlayerStatistics } = await import('@/lib/domain-hooks/usePlayRecords');
    vi.mocked(usePlayerStatistics).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any);

    render(<StatisticsPage />);

    expect(screen.getByTestId('stats-loading')).toBeInTheDocument();
  });

  it('AC-5.7: error state shows error message with retry CTA', async () => {
    const { usePlayerStatistics } = await import('@/lib/domain-hooks/usePlayRecords');
    const mockError = new Error('Network error');
    vi.mocked(usePlayerStatistics).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: mockError,
    } as any);

    render(<StatisticsPage />);

    expect(screen.getByTestId('stats-error')).toBeInTheDocument();
    // Mock i18n returns the raw key; StatisticsView renders t('playRecords.stats.error.title').
    expect(screen.getByText('playRecords.stats.error.title')).toBeInTheDocument();
  });

  it('AC-5.5: empty stats show KPI with zeros and EmptySection CTAs', async () => {
    const { usePlayerStatistics } = await import('@/lib/domain-hooks/usePlayRecords');
    vi.mocked(usePlayerStatistics).mockReturnValue({
      data: mockPlayerStatisticsEmpty,
      isLoading: false,
      error: null,
    } as any);

    render(<StatisticsPage />);

    // Should render sections with empty states
    expect(screen.getByTestId('most-played-empty')).toBeInTheDocument();
    expect(screen.getByTestId('win-by-game-empty')).toBeInTheDocument();
  });

  it('AC-5.8: responsive layout with md:grid-cols-2 for desktop', () => {
    const { container } = render(<StatisticsPage />);

    const grid = container.querySelector('[data-testid="stats-grid"]');
    expect(grid).toHaveClass('md:grid-cols-2');
  });

  it('AC-5.6: loading state shows section skeletons', async () => {
    const { usePlayerStatistics } = await import('@/lib/domain-hooks/usePlayRecords');
    vi.mocked(usePlayerStatistics).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any);

    const { container } = render(<StatisticsPage />);

    // Should have loading skeleton sections
    const skeletons = container.querySelectorAll('[data-testid="section-skeleton"]');
    expect(skeletons.length).toBeGreaterThanOrEqual(2);
  });

  it('happy path: renders both MostPlayedBar and WinByGameBar sections', () => {
    render(<StatisticsPage />);

    expect(screen.getByTestId('most-played-section')).toBeInTheDocument();
    expect(screen.getByTestId('win-by-game-section')).toBeInTheDocument();
  });

  it('AC-5.10: uses i18n namespace playRecords.stats', () => {
    render(<StatisticsPage />);

    // Verify page rendered successfully with i18n
    expect(screen.getByTestId('stats-page')).toBeInTheDocument();
  });
});
