/**
 * ChatHeader Component Tests - Issue #2232
 *
 * Tests for chat header component.
 * Features tested:
 * - Game selector dropdown
 * - Inline editable thread title
 * - Actions menu (Share, Export, Delete)
 * - Mobile menu trigger integration
 * - Loading states
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatHeader, type ChatHeaderProps } from '../ChatHeader';
import { Game } from '@/types';

const mockGames: Game[] = [
  { id: 'game-1', title: 'Catan', bggId: 13, year: 1995, description: '' },
  { id: 'game-2', title: 'Pandemic', bggId: 30549, year: 2008, description: '' },
  { id: 'game-3', title: 'Ticket to Ride', bggId: 9209, year: 2004, description: '' },
];

const defaultProps: ChatHeaderProps = {
  game: mockGames[0],
  games: mockGames,
  onGameChange: vi.fn(),
  threadTitle: 'My Chat Thread',
  onTitleChange: vi.fn(),
  onShare: vi.fn(),
  onExport: vi.fn(),
  onDelete: vi.fn(),
};

describe('ChatHeader', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic rendering', () => {
    it('renders header element', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('renders game selector with current game', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders thread title', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByText('My Chat Thread')).toBeInTheDocument();
    });

    it('renders actions menu button', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByLabelText('Thread actions menu')).toBeInTheDocument();
    });
  });

  describe('Game selector', () => {
    it('displays all available games in dropdown', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} />);

      // Open dropdown
      const trigger = screen.getByRole('combobox', { name: 'Select game' });
      await user.click(trigger);

      // All games should be visible (use getAllByText since "Catan" appears in trigger too)
      expect(screen.getAllByText('Catan').length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Pandemic')).toBeInTheDocument();
      expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    });

    it('calls onGameChange when game is selected', async () => {
      const user = userEvent.setup();
      const onGameChange = vi.fn();
      render(<ChatHeader {...defaultProps} onGameChange={onGameChange} />);

      // Open dropdown and select game
      const trigger = screen.getByRole('combobox', { name: 'Select game' });
      await user.click(trigger);
      await user.click(screen.getByText('Pandemic'));

      expect(onGameChange).toHaveBeenCalledWith('game-2');
    });

    it('is disabled when loading', () => {
      render(<ChatHeader {...defaultProps} loading={{ games: true }} />);

      const trigger = screen.getByRole('combobox', { name: 'Select game' });
      expect(trigger).toBeDisabled();
    });

    it('does not render when games list is empty', () => {
      render(<ChatHeader {...defaultProps} games={[]} />);

      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });
  });

  describe('Thread title editing', () => {
    it('shows title as button initially', () => {
      render(<ChatHeader {...defaultProps} />);

      const titleButton = screen.getByLabelText('Click to edit thread title');
      expect(titleButton).toBeInTheDocument();
    });

    it('enters edit mode on title click', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} />);

      const titleButton = screen.getByLabelText('Click to edit thread title');
      await user.click(titleButton);

      // Input should appear
      expect(screen.getByPlaceholderText('Thread title')).toBeInTheDocument();
    });

    it('shows save and cancel buttons in edit mode', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));

      expect(screen.getByLabelText('Save title')).toBeInTheDocument();
      expect(screen.getByLabelText('Cancel editing')).toBeInTheDocument();
    });

    it('calls onTitleChange on save with new title', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      // Enter edit mode
      await user.click(screen.getByLabelText('Click to edit thread title'));

      // Change title
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'New Title');

      // Save
      await user.click(screen.getByLabelText('Save title'));

      expect(onTitleChange).toHaveBeenCalledWith('New Title');
    });

    it('does not call onTitleChange if title unchanged', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      await user.click(screen.getByLabelText('Save title'));

      expect(onTitleChange).not.toHaveBeenCalled();
    });

    it('trims whitespace from new title', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, '  Trimmed Title  ');
      await user.click(screen.getByLabelText('Save title'));

      expect(onTitleChange).toHaveBeenCalledWith('Trimmed Title');
    });

    it('cancels edit on cancel button click', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'Changed');
      await user.click(screen.getByLabelText('Cancel editing'));

      expect(onTitleChange).not.toHaveBeenCalled();
      expect(screen.getByText('My Chat Thread')).toBeInTheDocument();
    });

    it('saves on Enter key press', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'New Title{Enter}');

      expect(onTitleChange).toHaveBeenCalledWith('New Title');
    });

    it('cancels on Escape key press', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'Changed{Escape}');

      expect(onTitleChange).not.toHaveBeenCalled();
    });

    it('is disabled when loading', () => {
      render(<ChatHeader {...defaultProps} loading={{ title: true }} />);

      const titleButton = screen.getByLabelText('Click to edit thread title');
      expect(titleButton).toBeDisabled();
    });
  });

  describe('Actions menu', () => {
    it('opens actions menu on button click', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} />);

      const menuButton = screen.getByLabelText('Thread actions menu');
      await user.click(menuButton);

      expect(screen.getByText('Share Thread')).toBeInTheDocument();
      expect(screen.getByText('Export Chat')).toBeInTheDocument();
      expect(screen.getByText('Delete Thread')).toBeInTheDocument();
    });

    it('calls onShare when Share is clicked', async () => {
      const user = userEvent.setup();
      const onShare = vi.fn();
      render(<ChatHeader {...defaultProps} onShare={onShare} />);

      await user.click(screen.getByLabelText('Thread actions menu'));
      await user.click(screen.getByText('Share Thread'));

      expect(onShare).toHaveBeenCalledTimes(1);
    });

    it('calls onExport when Export is clicked', async () => {
      const user = userEvent.setup();
      const onExport = vi.fn();
      render(<ChatHeader {...defaultProps} onExport={onExport} />);

      await user.click(screen.getByLabelText('Thread actions menu'));
      await user.click(screen.getByText('Export Chat'));

      expect(onExport).toHaveBeenCalledTimes(1);
    });

    it('calls onDelete when Delete is clicked', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<ChatHeader {...defaultProps} onDelete={onDelete} />);

      await user.click(screen.getByLabelText('Thread actions menu'));
      await user.click(screen.getByText('Delete Thread'));

      expect(onDelete).toHaveBeenCalledTimes(1);
    });

    it('does not render Share if onShare is undefined', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} onShare={undefined} />);

      await user.click(screen.getByLabelText('Thread actions menu'));

      expect(screen.queryByText('Share Thread')).not.toBeInTheDocument();
    });

    it('does not render Export if onExport is undefined', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} onExport={undefined} />);

      await user.click(screen.getByLabelText('Thread actions menu'));

      expect(screen.queryByText('Export Chat')).not.toBeInTheDocument();
    });

    it('does not render Delete if onDelete is undefined', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} onDelete={undefined} />);

      await user.click(screen.getByLabelText('Thread actions menu'));

      expect(screen.queryByText('Delete Thread')).not.toBeInTheDocument();
    });
  });

  describe('Mobile menu trigger', () => {
    it('renders mobile menu trigger when provided', () => {
      const trigger = <button data-testid="mobile-trigger">Menu</button>;
      render(<ChatHeader {...defaultProps} mobileMenuTrigger={trigger} />);

      expect(screen.getByTestId('mobile-trigger')).toBeInTheDocument();
    });

    it('does not render mobile trigger area when not provided', () => {
      render(<ChatHeader {...defaultProps} mobileMenuTrigger={undefined} />);

      // Should not have the mobile trigger container
      expect(screen.queryByTestId('mobile-trigger')).not.toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      const { container } = render(<ChatHeader {...defaultProps} className="custom-header" />);

      expect(container.querySelector('.custom-header')).toBeInTheDocument();
    });

    it('has sticky positioning', () => {
      const { container } = render(<ChatHeader {...defaultProps} />);

      expect(container.querySelector('.sticky')).toBeInTheDocument();
      expect(container.querySelector('.top-0')).toBeInTheDocument();
    });

    it('has proper z-index for layering', () => {
      const { container } = render(<ChatHeader {...defaultProps} />);

      expect(container.querySelector('.z-40')).toBeInTheDocument();
    });
  });

  describe('Edge cases', () => {
    it('handles undefined game gracefully', () => {
      render(<ChatHeader {...defaultProps} game={undefined} />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('handles empty games array', () => {
      render(<ChatHeader {...defaultProps} games={[]} />);

      // Game selector should not render
      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });

    it('handles undefined threadTitle with fallback', () => {
      render(<ChatHeader {...defaultProps} threadTitle={undefined} />);

      expect(screen.getByText('Untitled Thread')).toBeInTheDocument();
    });

    it('handles all handlers undefined', () => {
      render(
        <ChatHeader
          {...defaultProps}
          onGameChange={undefined}
          onTitleChange={undefined}
          onShare={undefined}
          onExport={undefined}
          onDelete={undefined}
        />
      );

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA label on header', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(
        screen.getByRole('banner', { name: 'Chat header with game selection and thread actions' })
      ).toBeInTheDocument();
    });

    it('has accessible game selector', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByLabelText('Select game')).toBeInTheDocument();
    });

    it('has accessible title edit button', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByLabelText('Click to edit thread title')).toBeInTheDocument();
    });

    it('has accessible actions menu', () => {
      render(<ChatHeader {...defaultProps} />);

      expect(screen.getByLabelText('Thread actions menu')).toBeInTheDocument();
    });

    it('edit mode buttons have ARIA labels', async () => {
      const user = userEvent.setup();
      render(<ChatHeader {...defaultProps} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));

      expect(screen.getByLabelText('Save title')).toBeInTheDocument();
      expect(screen.getByLabelText('Cancel editing')).toBeInTheDocument();
    });
  });

  describe('Keyboard navigation', () => {
    it('supports Enter to save title', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'Keyboard Title');
      await user.keyboard('{Enter}');

      expect(onTitleChange).toHaveBeenCalledWith('Keyboard Title');
    });

    it('supports Escape to cancel editing', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'Changed');
      await user.keyboard('{Escape}');

      expect(onTitleChange).not.toHaveBeenCalled();
      expect(screen.getByText('My Chat Thread')).toBeInTheDocument();
    });
  });

  describe('Loading states', () => {
    it('disables game selector when games are loading', () => {
      render(<ChatHeader {...defaultProps} loading={{ games: true }} />);

      const selector = screen.getByRole('combobox', { name: 'Select game' });
      expect(selector).toBeDisabled();
    });

    it('disables title editing when title is loading', () => {
      render(<ChatHeader {...defaultProps} loading={{ title: true }} />);

      const titleButton = screen.getByLabelText('Click to edit thread title');
      expect(titleButton).toBeDisabled();
    });

    it('shows loading cursor for title when loading', () => {
      render(<ChatHeader {...defaultProps} loading={{ title: true }} />);

      const titleButton = screen.getByLabelText('Click to edit thread title');
      expect(titleButton).toHaveClass('cursor-wait');
    });
  });

  describe('Input validation', () => {
    it('does not save empty title', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.click(screen.getByLabelText('Save title'));

      expect(onTitleChange).not.toHaveBeenCalled();
    });

    it('does not save whitespace-only title', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatHeader {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, '   ');
      await user.click(screen.getByLabelText('Save title'));

      expect(onTitleChange).not.toHaveBeenCalled();
    });
  });

  describe('Responsive design', () => {
    it('hides mobile menu trigger on desktop', () => {
      const trigger = <button data-testid="mobile-trigger">Menu</button>;
      render(<ChatHeader {...defaultProps} mobileMenuTrigger={trigger} />);

      const triggerContainer = screen.getByTestId('mobile-trigger').parentElement;
      expect(triggerContainer).toHaveClass('md:hidden');
    });

    it('game selector has responsive width', () => {
      render(<ChatHeader {...defaultProps} />);

      const trigger = screen.getByRole('combobox', { name: 'Select game' });
      expect(trigger).toHaveClass('w-[180px]');
      expect(trigger).toHaveClass('sm:w-[220px]');
    });
  });
});
