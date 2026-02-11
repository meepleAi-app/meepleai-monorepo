/**
 * MeepleCardQuickActions Tests
 * Issue #4033 - Comprehensive Testing
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageSquare, Play, Share2 } from 'lucide-react';
import { describe, it, expect, vi } from 'vitest';

import { MeepleCardQuickActions } from '../meeple-card-quick-actions';

describe('MeepleCardQuickActions', () => {
  const mockActions = [
    {
      icon: MessageSquare,
      label: 'Chat',
      onClick: vi.fn(),
    },
    {
      icon: Play,
      label: 'Start',
      onClick: vi.fn(),
    },
    {
      icon: Share2,
      label: 'Share',
      onClick: vi.fn(),
    },
  ];

  it('renders all action buttons', () => {
    render(
      <div className="group">
        <MeepleCardQuickActions actions={mockActions} entityType="game" />
      </div>
    );

    expect(screen.getByLabelText('Chat')).toBeInTheDocument();
    expect(screen.getByLabelText('Start')).toBeInTheDocument();
    expect(screen.getByLabelText('Share')).toBeInTheDocument();
  });

  it('calls onClick when button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <div className="group">
        <MeepleCardQuickActions actions={mockActions} entityType="game" />
      </div>
    );

    await user.click(screen.getByLabelText('Chat'));
    expect(mockActions[0].onClick).toHaveBeenCalledTimes(1);
  });

  it('filters out hidden actions', () => {
    const actionsWithHidden = [
      ...mockActions,
      { icon: Play, label: 'Hidden', onClick: vi.fn(), hidden: true },
    ];

    render(
      <div className="group">
        <MeepleCardQuickActions actions={actionsWithHidden} entityType="game" />
      </div>
    );

    expect(screen.queryByLabelText('Hidden')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button')).toHaveLength(3);
  });

  it('returns null when no visible actions', () => {
    const { container } = render(
      <MeepleCardQuickActions actions={[]} entityType="game" />
    );

    expect(container.firstChild).toBeNull();
  });

  it('disables buttons when disabled prop is true', () => {
    const disabledActions = [
      { icon: MessageSquare, label: 'Disabled', onClick: vi.fn(), disabled: true },
    ];

    render(
      <div className="group">
        <MeepleCardQuickActions actions={disabledActions} entityType="game" />
      </div>
    );

    expect(screen.getByLabelText('Disabled')).toBeDisabled();
  });

  it('stops event propagation on click', async () => {
    const user = userEvent.setup();
    const parentClick = vi.fn();

    render(
      <div onClick={parentClick} className="group">
        <MeepleCardQuickActions actions={mockActions} entityType="game" />
      </div>
    );

    await user.click(screen.getByLabelText('Chat'));
    expect(mockActions[0].onClick).toHaveBeenCalled();
    expect(parentClick).not.toHaveBeenCalled();
  });

  it('applies entity-colored styling', () => {
    const { container } = render(
      <div className="group">
        <MeepleCardQuickActions actions={[mockActions[0]]} entityType="game" />
      </div>
    );

    const button = screen.getByLabelText('Chat');
    expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(25 95% 45%)' });
  });

  it('uses custom color when provided', () => {
    const { container } = render(
      <div className="group">
        <MeepleCardQuickActions
          actions={[mockActions[0]]}
          entityType="game"
          customColor="200 50% 50%"
        />
      </div>
    );

    const button = screen.getByLabelText('Chat');
    expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(200 50% 50%)' });
  });
});
