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
  hasFailedPdfs: false,
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

  it('shows drop zone with max file info', () => {
    render(<UploadForGameDrawer game={mockGame} open={true} onClose={vi.fn()} />);

    expect(screen.getByText(/trascina i pdf qui/i)).toBeInTheDocument();
    expect(screen.getByText(/max 5 file/i)).toBeInTheDocument();
  });
});
