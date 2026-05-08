import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import * as hookModule from '@/lib/gamebook/hooks/useUserCampaigns';
import type { UserCampaignWithStale } from '@/lib/gamebook/hooks/useUserCampaigns';

import { GamebookResumeShell } from '../GamebookResumeShell';

vi.mock('@/lib/gamebook/hooks/useUserCampaigns', async () => {
  const actual = await vi.importActual<typeof hookModule>(
    '@/lib/gamebook/hooks/useUserCampaigns'
  );
  return { ...actual, useUserCampaigns: vi.fn() };
});

function buildCampaign(overrides: Partial<UserCampaignWithStale> = {}): UserCampaignWithStale {
  return {
    id: overrides.id ?? 'c1',
    gameId: 'g1',
    ownerUserId: 'u1',
    title: overrides.title ?? 'Veglia di Brace',
    currentParagraph: overrides.currentParagraph ?? 289,
    history: overrides.history ?? [200, 250, 289],
    lastReadAt: overrides.lastReadAt ?? '2026-04-30T20:00:00Z',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: overrides.lastReadAt ?? '2026-04-30T20:00:00Z',
    isStale: overrides.isStale ?? false,
  };
}

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('GamebookResumeShell — dispatch logic (mockup G 4 stati)', () => {
  it('renders state-01-first-time when data is empty', () => {
    vi.mocked(hookModule.useUserCampaigns).mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
    } as never);

    wrap(<GamebookResumeShell gameId="g1" gameTitle="Nanolith" />);

    expect(screen.getByTestId('gamebook-resume-empty-first-time')).toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-hero')).not.toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-multi-list')).not.toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-stale-warning')).not.toBeInTheDocument();
  });

  it('renders state-02-single-resume when 1 campaign and not stale', () => {
    vi.mocked(hookModule.useUserCampaigns).mockReturnValue({
      data: [buildCampaign({ isStale: false })],
      isLoading: false,
      isError: false,
    } as never);

    wrap(<GamebookResumeShell gameId="g1" />);

    expect(screen.getByTestId('gamebook-resume-hero')).toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-stale-warning')).not.toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-multi-list')).not.toBeInTheDocument();
  });

  it('renders state-03-multi-campaign when 2+ campaigns', () => {
    vi.mocked(hookModule.useUserCampaigns).mockReturnValue({
      data: [
        buildCampaign({ id: 'c1', title: 'Veglia di Brace' }),
        buildCampaign({ id: 'c2', title: 'La Caduta di Avalon' }),
      ],
      isLoading: false,
      isError: false,
    } as never);

    wrap(<GamebookResumeShell gameId="g1" />);

    expect(screen.getByTestId('gamebook-resume-multi-list')).toBeInTheDocument();
    expect(screen.getByTestId('gamebook-resume-multi-item-c1')).toBeInTheDocument();
    expect(screen.getByTestId('gamebook-resume-multi-item-c2')).toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-hero')).not.toBeInTheDocument();
  });

  it('renders state-04-stale-warning when 1 campaign and isStale', () => {
    vi.mocked(hookModule.useUserCampaigns).mockReturnValue({
      data: [buildCampaign({ isStale: true })],
      isLoading: false,
      isError: false,
    } as never);

    wrap(<GamebookResumeShell gameId="g1" />);

    expect(screen.getByTestId('gamebook-resume-stale-warning')).toBeInTheDocument();
    expect(screen.queryByTestId('gamebook-resume-hero')).not.toBeInTheDocument();
  });

  it('renders skeleton while loading', () => {
    vi.mocked(hookModule.useUserCampaigns).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    } as never);

    wrap(<GamebookResumeShell gameId="g1" />);

    expect(screen.getByTestId('gamebook-resume-shell-skeleton')).toBeInTheDocument();
  });

  it('renders error state when isError', () => {
    vi.mocked(hookModule.useUserCampaigns).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    } as never);

    wrap(<GamebookResumeShell gameId="g1" />);

    expect(screen.getByTestId('gamebook-resume-shell-error')).toBeInTheDocument();
  });
});
