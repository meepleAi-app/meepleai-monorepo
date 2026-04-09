import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { TopBarSearchPill } from '../TopBarSearchPill';

describe('TopBarSearchPill', () => {
  it('shows placeholder text and ⌘K hint', () => {
    render(<TopBarSearchPill />);
    expect(screen.getByText(/cerca giochi/i)).toBeInTheDocument();
    expect(screen.getByText('⌘K')).toBeInTheDocument();
  });

  it('invokes onOpen when clicked', async () => {
    const onOpen = vi.fn();
    const user = userEvent.setup();
    render(<TopBarSearchPill onOpen={onOpen} />);
    await user.click(screen.getByRole('button', { name: /search/i }));
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('invokes onOpen on ⌘K keyboard shortcut', async () => {
    const onOpen = vi.fn();
    render(<TopBarSearchPill onOpen={onOpen} />);
    // Simulate Cmd+K
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true, bubbles: true }));
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('invokes onOpen on Ctrl+K keyboard shortcut', async () => {
    const onOpen = vi.fn();
    render(<TopBarSearchPill onOpen={onOpen} />);
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }));
    expect(onOpen).toHaveBeenCalledOnce();
  });
});
