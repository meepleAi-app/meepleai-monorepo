import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';

import { UploadForGameDrawer } from '../upload-for-game-drawer';

const mockGame: GameWithoutKbDto = {
  gameId: 'aaa-111',
  title: 'Wingspan',
  publisher: 'Stonemaier Games',
  imageUrl: null,
  playerCountLabel: '1–5 giocatori',
  pdfCount: 0,
  failedPdfCount: 0,
};

describe('UploadForGameDrawer', () => {
  it('renders game title and publisher in the drawer', () => {
    render(<UploadForGameDrawer game={mockGame} open={true} onClose={vi.fn()} />);

    expect(screen.getByText(/Upload PDF — Wingspan/)).toBeInTheDocument();
    expect(screen.getByText('Stonemaier Games')).toBeInTheDocument();
  });

  it('calls onClose when Annulla is clicked', async () => {
    const onClose = vi.fn();
    render(<UploadForGameDrawer game={mockGame} open={true} onClose={onClose} />);

    await userEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('does not render content when open=false', () => {
    render(<UploadForGameDrawer game={mockGame} open={false} onClose={vi.fn()} />);

    expect(screen.queryByText(/Upload PDF — Wingspan/)).not.toBeInTheDocument();
  });

  it('returns null when no game is provided', () => {
    const { container } = render(<UploadForGameDrawer game={null} open={true} onClose={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('shows CTA linking to upload page with gameId pre-filled', () => {
    render(<UploadForGameDrawer game={mockGame} open={true} onClose={vi.fn()} />);

    const cta = screen.getByRole('link', { name: /apri flusso di upload/i });
    expect(cta).toHaveAttribute('href', '/admin/knowledge-base/upload?gameId=aaa-111');
  });

  it('surfaces failedPdfCount in metadata when > 0', () => {
    const failed: GameWithoutKbDto = {
      ...mockGame,
      pdfCount: 3,
      failedPdfCount: 2,
    };
    render(<UploadForGameDrawer game={failed} open={true} onClose={vi.fn()} />);

    expect(screen.getByText('PDF falliti')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });
});
