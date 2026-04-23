import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ConnectionChip } from '../ConnectionChip';

describe('ConnectionChip', () => {
  it('renders count badge when count > 0', () => {
    render(<ConnectionChip entityType="session" count={5} />);
    expect(screen.getByTestId('connection-chip-badge')).toHaveTextContent('5');
  });

  it('renders "99+" when count > 99', () => {
    render(<ConnectionChip entityType="kb" count={150} />);
    expect(screen.getByTestId('connection-chip-badge')).toHaveTextContent('99+');
  });

  it('omits badge when count is 0', () => {
    render(<ConnectionChip entityType="chat" count={0} />);
    expect(screen.queryByTestId('connection-chip-badge')).not.toBeInTheDocument();
  });

  it('shows plus overlay when count=0 and onCreate provided', () => {
    render(<ConnectionChip entityType="player" count={0} onCreate={() => {}} />);
    expect(screen.getByTestId('connection-chip-plus')).toBeInTheDocument();
  });

  it('omits plus overlay when count=0 and no onCreate', () => {
    render(<ConnectionChip entityType="player" count={0} />);
    expect(screen.queryByTestId('connection-chip-plus')).not.toBeInTheDocument();
  });

  it('calls onCreate when empty chip is clicked', async () => {
    const onCreate = vi.fn();
    render(<ConnectionChip entityType="player" count={0} onCreate={onCreate} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('is not clickable when disabled', async () => {
    const onCreate = vi.fn();
    render(<ConnectionChip entityType="player" count={0} onCreate={onCreate} disabled />);
    const btn = screen.getByRole('button');
    expect(btn).toBeDisabled();
    await userEvent.click(btn);
    expect(onCreate).not.toHaveBeenCalled();
  });

  it('renders skeleton when loading=true', () => {
    render(<ConnectionChip entityType="kb" loading />);
    expect(screen.getByTestId('connection-chip-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('connection-chip-badge')).not.toBeInTheDocument();
  });

  it('renders a Lucide SVG icon (not emoji) for the entity', () => {
    const { container } = render(<ConnectionChip entityType="game" count={3} />);
    expect(container.querySelector('svg')).toBeTruthy();
  });

  it('shows label under chip when showLabel=true', () => {
    render(<ConnectionChip entityType="kb" count={3} showLabel label="Docs" />);
    expect(screen.getByText('Docs')).toBeInTheDocument();
  });

  it('opens popover when clicked and items are present', async () => {
    render(
      <ConnectionChip
        entityType="session"
        count={2}
        items={[
          { id: '1', label: 'First', href: '/sessions/1' },
          { id: '2', label: 'Second', href: '/sessions/2' },
        ]}
      />
    );
    await userEvent.click(screen.getByRole('button'));
    expect(await screen.findByText('First')).toBeInTheDocument();
    expect(screen.getByText('Second')).toBeInTheDocument();
  });

  it('has aria-label including count and entity label', () => {
    render(<ConnectionChip entityType="session" count={5} />);
    const btn = screen.getByRole('button');
    expect(btn.getAttribute('aria-label')).toMatch(/5/);
    expect(btn.getAttribute('aria-label')?.toLowerCase()).toMatch(/session/);
  });

  it('renders as a link when href is provided and no items/popover', () => {
    render(<ConnectionChip entityType="kb" count={3} href="/kb/123" />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/kb/123');
    expect(link.getAttribute('aria-label')).toMatch(/3/);
  });
});
