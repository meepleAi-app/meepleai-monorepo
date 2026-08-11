import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { BlockedLiveSessionModal } from './BlockedLiveSessionModal';

describe('BlockedLiveSessionModal', () => {
  it('renders nothing when closed', () => {
    render(<BlockedLiveSessionModal open={false} onClose={() => undefined} />);
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('renders the honest max-live copy when open', () => {
    render(<BlockedLiveSessionModal open onClose={() => undefined} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /già una partita live/i })).toBeInTheDocument();
    expect(screen.getByText(/una sola partita live/i)).toBeInTheDocument();
  });

  it('renders the jump-to-live action only when onJumpToLive is provided', () => {
    const { rerender } = render(<BlockedLiveSessionModal open onClose={() => undefined} />);
    expect(screen.queryByRole('button', { name: /Apri la partita live/i })).toBeNull();

    rerender(
      <BlockedLiveSessionModal open onClose={() => undefined} onJumpToLive={() => undefined} />
    );
    expect(screen.getByRole('button', { name: /Apri la partita live/i })).toBeInTheDocument();
  });

  it('invokes onJumpToLive on the jump button', async () => {
    const onJumpToLive = vi.fn();
    render(<BlockedLiveSessionModal open onClose={() => undefined} onJumpToLive={onJumpToLive} />);
    await userEvent.click(screen.getByRole('button', { name: /Apri la partita live/i }));
    expect(onJumpToLive).toHaveBeenCalledTimes(1);
  });

  it('invokes onClose on the Chiudi button and on Escape', async () => {
    const onClose = vi.fn();
    render(<BlockedLiveSessionModal open onClose={onClose} />);
    await userEvent.click(screen.getByRole('button', { name: 'Chiudi' }));
    expect(onClose).toHaveBeenCalledTimes(1);

    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(2);
  });
});
