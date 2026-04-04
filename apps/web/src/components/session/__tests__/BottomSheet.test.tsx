import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { BottomSheet } from '../BottomSheet';

describe('BottomSheet', () => {
  it('renders content when open', () => {
    render(
      <BottomSheet isOpen onClose={vi.fn()} title="Test Title">
        <p>Sheet content</p>
      </BottomSheet>
    );
    expect(screen.getByText('Test Title')).toBeInTheDocument();
    expect(screen.getByText('Sheet content')).toBeInTheDocument();
  });

  it('does not render content when closed', () => {
    render(
      <BottomSheet isOpen={false} onClose={vi.fn()} title="Hidden Title">
        <p>Hidden content</p>
      </BottomSheet>
    );
    expect(screen.queryByText('Hidden Title')).not.toBeInTheDocument();
    expect(screen.queryByText('Hidden content')).not.toBeInTheDocument();
  });

  it('calls onClose when Escape key is pressed', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(
      <BottomSheet isOpen onClose={onClose} title="Closable">
        <p>Press Escape</p>
      </BottomSheet>
    );
    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
