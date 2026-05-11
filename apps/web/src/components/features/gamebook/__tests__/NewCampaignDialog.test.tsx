import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import * as client from '@/lib/api/gamebook-campaigns';

import { NewCampaignDialog } from '../NewCampaignDialog';

vi.mock('@/lib/api/gamebook-campaigns');
const pushSpy = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushSpy }),
}));

function wrap(ui: ReactNode) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  pushSpy.mockReset();
  vi.clearAllMocks();
});

describe('NewCampaignDialog', () => {
  it('opens dialog, calls createCampaign, navigates on success', async () => {
    vi.mocked(client.createCampaign).mockResolvedValueOnce({
      id: 'new-c1',
      gameId: 'g1',
      ownerUserId: 'u',
      title: 'My Title',
      currentParagraph: 0,
      history: [],
      lastReadAt: '2026-05-07T12:00:00Z',
      createdAt: '2026-05-07T12:00:00Z',
      updatedAt: '2026-05-07T12:00:00Z',
    } as never);

    wrap(<NewCampaignDialog gameId="g1" trigger={<button>Apri</button>} />);

    fireEvent.click(screen.getByRole('button', { name: 'Apri' }));
    fireEvent.change(screen.getByTestId('new-campaign-title-input'), {
      target: { value: 'My Title' },
    });
    fireEvent.click(screen.getByTestId('new-campaign-submit'));

    await waitFor(() =>
      expect(client.createCampaign).toHaveBeenCalledWith({ gameId: 'g1', title: 'My Title' })
    );
    await waitFor(() => expect(pushSpy).toHaveBeenCalledWith('/library/games/g1/play/new-c1'));
  });

  it('disables submit when title is empty', () => {
    wrap(<NewCampaignDialog gameId="g1" trigger={<button>Apri</button>} />);
    fireEvent.click(screen.getByRole('button', { name: 'Apri' }));
    expect(screen.getByTestId('new-campaign-submit')).toBeDisabled();
  });
});
