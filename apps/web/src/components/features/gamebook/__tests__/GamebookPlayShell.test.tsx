import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import * as hookModule from '@/lib/gamebook/hooks/useGamebookCampaign';
import * as mutModule from '@/lib/gamebook/hooks/useUpdateGamebookProgress';

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import { GamebookPlayShell } from '../GamebookPlayShell';

vi.mock('@/lib/gamebook/hooks/useGamebookCampaign');
vi.mock('@/lib/gamebook/hooks/useUpdateGamebookProgress');
vi.mock('@/lib/stores/chat-panel-store', () => ({
  useChatPanelStore: (selector: (s: { open: () => void }) => unknown) =>
    selector({ open: vi.fn() }),
}));

const fakeCampaign = {
  id: 'c1',
  gameId: 'g1',
  ownerUserId: 'u1',
  title: 'Campagna #1',
  currentParagraph: 47,
  history: [42, 45, 47],
  lastReadAt: '2026-05-07T12:00:00Z',
  createdAt: '2026-05-01T00:00:00Z',
  updatedAt: '2026-05-07T12:00:00Z',
};

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(mutModule.useUpdateGamebookProgress).mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
  } as never);
});

describe('GamebookPlayShell', () => {
  it('renders campaign title and current paragraph badge', () => {
    vi.mocked(hookModule.useGamebookCampaign).mockReturnValue({
      data: fakeCampaign,
      isLoading: false,
    } as never);

    wrap(<GamebookPlayShell campaignId="c1" gameId="g1" agentId="a1" />);

    expect(screen.getByText('Campagna #1')).toBeInTheDocument();
    expect(screen.getByText(/§\s*47/)).toBeInTheDocument();
    expect(screen.getByTestId('gamebook-open-chat')).toBeInTheDocument();
  });

  it('renders paragraph dash when currentParagraph is 0', () => {
    vi.mocked(hookModule.useGamebookCampaign).mockReturnValue({
      data: { ...fakeCampaign, currentParagraph: 0, history: [] },
      isLoading: false,
    } as never);

    wrap(<GamebookPlayShell campaignId="c1" gameId="g1" />);

    expect(screen.getByText(/§\s*—/)).toBeInTheDocument();
  });

  it('shows skeleton while loading', () => {
    vi.mocked(hookModule.useGamebookCampaign).mockReturnValue({
      data: undefined,
      isLoading: true,
    } as never);
    wrap(<GamebookPlayShell campaignId="c1" gameId="g1" />);
    expect(screen.getByTestId('gamebook-play-shell-skeleton')).toBeInTheDocument();
  });

  it('renders Translate CTA with correct href', () => {
    vi.mocked(hookModule.useGamebookCampaign).mockReturnValue({
      data: fakeCampaign,
      isLoading: false,
    } as never);

    wrap(<GamebookPlayShell campaignId="c1" gameId="g1" />);

    const link = screen.getByTestId('gamebook-open-translate');
    expect(link).toBeInTheDocument();
    expect(link.closest('a')?.href).toContain('/library/g1/play/c1/translate');
  });
});
