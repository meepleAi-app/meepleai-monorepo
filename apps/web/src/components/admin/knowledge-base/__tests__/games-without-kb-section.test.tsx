import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { GamesWithoutKbSection } from '../games-without-kb-section';

vi.mock('@/lib/api/kb-games-without-kb-api', () => ({
  useGamesWithoutKb: vi.fn(),
}));

import { useGamesWithoutKb } from '@/lib/api/kb-games-without-kb-api';

const mockGames = [
  {
    gameId: 'aaa-111',
    title: 'Catan',
    publisher: 'Kosmos',
    imageUrl: null,
    playerCountLabel: '3–4 giocatori',
    pdfCount: 0,
    failedPdfCount: 0,
  },
  {
    gameId: 'bbb-222',
    title: 'Pandemic',
    publisher: null,
    imageUrl: null,
    playerCountLabel: '2–4 giocatori',
    pdfCount: 3,
    failedPdfCount: 2,
  },
];

describe('GamesWithoutKbSection', () => {
  beforeEach(() => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: { items: mockGames, total: 2, page: 1, pageSize: 20, totalPages: 1 },
      isLoading: false,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);
  });

  it('renders a MeepleCard for each game', () => {
    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('shows failed badge with plural form when multiple PDFs failed', () => {
    // Pandemic has pdfCount=3, failedPdfCount=2 — badge must reflect the failed
    // count (2), NOT the total, and use plural form "falliti" (regression for H3+M3).
    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText('2 falliti')).toBeInTheDocument();
    expect(screen.queryByText('3 fallito')).not.toBeInTheDocument();
    expect(screen.queryByText('3 falliti')).not.toBeInTheDocument();
  });

  it('uses singular form when exactly 1 PDF failed', () => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: {
        items: [
          {
            gameId: 'ccc-333',
            title: 'Root',
            publisher: null,
            imageUrl: null,
            playerCountLabel: '2–4 giocatori',
            pdfCount: 1,
            failedPdfCount: 1,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      },
      isLoading: false,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);

    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText('1 fallito')).toBeInTheDocument();
  });

  it('calls onUploadClick with the game when action button is clicked', async () => {
    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    const buttons = screen.getAllByRole('button', { name: /aggiungi pdf/i });
    await userEvent.click(buttons[0]);

    expect(onUpload).toHaveBeenCalledWith(mockGames[0]);
  });

  it('shows loading skeletons when loading', () => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);

    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByTestId('games-without-kb-loading')).toBeInTheDocument();
  });

  it('shows empty state when no games found', () => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 },
      isLoading: false,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);

    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText(/tutti i giochi hanno una kb attiva/i)).toBeInTheDocument();
  });
});
