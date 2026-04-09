import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { HandRailToolbar } from '../HandRailToolbar';

describe('HandRailToolbar', () => {
  it('renders pin and expand buttons', () => {
    render(<HandRailToolbar onTogglePin={() => {}} onToggleExpand={() => {}} />);
    expect(screen.getByRole('button', { name: /pin/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /expand/i })).toBeInTheDocument();
  });

  it('calls onTogglePin when pin clicked', async () => {
    const onTogglePin = vi.fn();
    const user = userEvent.setup();
    render(<HandRailToolbar onTogglePin={onTogglePin} onToggleExpand={() => {}} />);
    await user.click(screen.getByRole('button', { name: /pin/i }));
    expect(onTogglePin).toHaveBeenCalledOnce();
  });

  it('calls onToggleExpand when expand clicked', async () => {
    const onToggleExpand = vi.fn();
    const user = userEvent.setup();
    render(<HandRailToolbar onTogglePin={() => {}} onToggleExpand={onToggleExpand} />);
    await user.click(screen.getByRole('button', { name: /expand/i }));
    expect(onToggleExpand).toHaveBeenCalledOnce();
  });
});
