import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { TopBarChatButton } from '../TopBarChatButton';

describe('TopBarChatButton', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  it('renders with accessible label', () => {
    render(<TopBarChatButton />);
    expect(screen.getByRole('button', { name: /chat agente/i })).toBeInTheDocument();
  });

  it('shows notification dot when hasUnread is true', () => {
    render(<TopBarChatButton hasUnread />);
    expect(screen.getByTestId('chat-unread-dot')).toBeInTheDocument();
  });

  it('hides notification dot when hasUnread is false', () => {
    render(<TopBarChatButton hasUnread={false} />);
    expect(screen.queryByTestId('chat-unread-dot')).not.toBeInTheDocument();
  });

  it('calls onOpen when clicked', async () => {
    const onOpen = vi.fn();
    const user = userEvent.setup();
    render(<TopBarChatButton onOpen={onOpen} />);
    await user.click(screen.getByRole('button', { name: /chat agente/i }));
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('warns if clicked without onOpen (Phase 1 stub)', async () => {
    const user = userEvent.setup();
    render(<TopBarChatButton />);
    await user.click(screen.getByRole('button', { name: /chat agente/i }));
    expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('TopBarChatButton'));
  });
});
