/**
 * ThreadListItem Component Tests (Issue #858)
 *
 * Tests for the new thread list item component including:
 * - Thread title display
 * - Status badges (active/archived)
 * - Message count and timestamp formatting
 * - Selection and deletion handlers
 * - Accessibility compliance
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThreadListItem } from '../ThreadListItem';
import { ChatThread } from '@/types';

describe('ThreadListItem', () => {
  const mockThread: ChatThread = {
    id: 'aa0e8400-e29b-41d4-a716-000000000001',
    userId: '990e8400-e29b-41d4-a716-000000000001',
    gameId: '770e8400-e29b-41d4-a716-000000000001',
    title: 'How to play Catan?',
    status: 'Active',
    createdAt: new Date('2025-11-15T10:00:00Z').toISOString(),
    lastMessageAt: new Date('2025-11-15T10:30:00Z').toISOString(),
    messageCount: 5,
    messages: [],
  };

  const mockHandlers = {
    onSelect: jest.fn(),
    onDelete: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders thread title correctly', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('How to play Catan?')).toBeInTheDocument();
    });

    it('renders default title when title is null', () => {
      const threadWithoutTitle = { ...mockThread, title: null };
      render(
        <ThreadListItem
          thread={threadWithoutTitle}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('New Chat')).toBeInTheDocument();
    });

    it('renders message count', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('renders archived badge for closed threads', () => {
      const archivedThread = { ...mockThread, status: 'Closed' };
      render(
        <ThreadListItem
          thread={archivedThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('Archived')).toBeInTheDocument();
      // Badge has aria-label but it's a span, not findable by getByLabelText in this context
      const badge = screen.getByText('Archived');
      expect(badge).toHaveAttribute('aria-label', 'Archived');
    });

    it('does not render archived badge for active threads', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.queryByText('Archived')).not.toBeInTheDocument();
    });

    it('applies active styling when isActive is true', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={true}
          {...mockHandlers}
        />
      );

      const container = screen.getByRole('listitem');
      const wrapper = container.querySelector('div');
      expect(wrapper).toHaveClass('bg-[#e8f0fe]', 'text-[#1a73e8]');
      expect(wrapper).toHaveAttribute('aria-current', 'true');
    });

    it('applies inactive styling when isActive is false', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const container = screen.getByRole('listitem');
      const wrapper = container.querySelector('div');
      expect(wrapper).toHaveClass('bg-transparent');
      expect(wrapper).not.toHaveAttribute('aria-current');
    });
  });

  describe('Timestamp Formatting', () => {
    it('formats timestamp as "Just now" for recent messages', () => {
      const nowThread = {
        ...mockThread,
        lastMessageAt: new Date().toISOString(),
      };
      render(
        <ThreadListItem
          thread={nowThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('Just now')).toBeInTheDocument();
    });

    it('formats timestamp as minutes for messages < 1 hour old', () => {
      const tenMinutesAgo = new Date(Date.now() - 10 * 60 * 1000);
      const recentThread = {
        ...mockThread,
        lastMessageAt: tenMinutesAgo.toISOString(),
      };
      render(
        <ThreadListItem
          thread={recentThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      // Text appears twice: in sr-only and visible span
      const timestamps = screen.getAllByText(/\d+m ago/);
      expect(timestamps.length).toBeGreaterThan(0);
    });

    it('formats timestamp as hours for messages < 24 hours old', () => {
      const threeHoursAgo = new Date(Date.now() - 3 * 60 * 60 * 1000);
      const recentThread = {
        ...mockThread,
        lastMessageAt: threeHoursAgo.toISOString(),
      };
      render(
        <ThreadListItem
          thread={recentThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const timestamps = screen.getAllByText(/\d+h ago/);
      expect(timestamps.length).toBeGreaterThan(0);
    });

    it('formats timestamp as days for messages < 7 days old', () => {
      const twoDaysAgo = new Date(Date.now() - 2 * 24 * 60 * 60 * 1000);
      const oldThread = {
        ...mockThread,
        lastMessageAt: twoDaysAgo.toISOString(),
      };
      render(
        <ThreadListItem
          thread={oldThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const timestamps = screen.getAllByText(/\d+d ago/);
      expect(timestamps.length).toBeGreaterThan(0);
    });

    it('formats timestamp as date for messages > 7 days old', () => {
      const tenDaysAgo = new Date(Date.now() - 10 * 24 * 60 * 60 * 1000);
      const oldThread = {
        ...mockThread,
        lastMessageAt: tenDaysAgo.toISOString(),
      };
      render(
        <ThreadListItem
          thread={oldThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      // Should show formatted date (appears in both sr-only and visible span)
      const dates = screen.getAllByText(/\d{1,2}\/\d{1,2}\/\d{4}/);
      expect(dates.length).toBeGreaterThan(0);
    });

    it('shows "No messages" when lastMessageAt is null', () => {
      const emptyThread = {
        ...mockThread,
        lastMessageAt: null,
      };
      render(
        <ThreadListItem
          thread={emptyThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('No messages')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('calls onSelect when thread is clicked', async () => {
      const user = userEvent.setup();
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const selectButton = screen.getByRole('button', { name: /Select thread:/ });
      await user.click(selectButton);

      expect(mockHandlers.onSelect).toHaveBeenCalledTimes(1);
    });

    it('calls onDelete when delete button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const deleteButton = screen.getByRole('button', { name: /Delete How to play Catan/ });
      await user.click(deleteButton);

      expect(mockHandlers.onDelete).toHaveBeenCalledTimes(1);
    });

    it('does not call onSelect when delete button is clicked (event stopPropagation)', async () => {
      const user = userEvent.setup();
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const deleteButton = screen.getByRole('button', { name: /Delete How to play Catan/ });
      await user.click(deleteButton);

      expect(mockHandlers.onDelete).toHaveBeenCalledTimes(1);
      expect(mockHandlers.onSelect).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA labels for screen readers', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const selectButton = screen.getByRole('button', { name: /Select thread:/ });
      expect(selectButton).toHaveAccessibleName();

      const deleteButton = screen.getByRole('button', { name: /Delete How to play Catan/ });
      expect(deleteButton).toHaveAccessibleName();
    });

    it('sets aria-current for active thread', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={true}
          {...mockHandlers}
        />
      );

      const container = screen.getByRole('listitem');
      const wrapper = container.querySelector('div');
      expect(wrapper).toHaveAttribute('aria-current', 'true');
    });

    it('does not set aria-current for inactive thread', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const container = screen.getByRole('listitem');
      const wrapper = container.querySelector('div');
      expect(wrapper).not.toHaveAttribute('aria-current');
    });

    it('has title attributes for tooltips', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const titleElement = screen.getByText('How to play Catan?');
      expect(titleElement).toHaveAttribute('title', 'How to play Catan?');

      const deleteButton = screen.getByRole('button', { name: /Delete/ });
      expect(deleteButton).toHaveAttribute('title', 'Delete thread');
    });
  });

  describe('Visual States', () => {
    it('shows archived thread with reduced opacity', () => {
      const archivedThread = { ...mockThread, status: 'Closed' };
      render(
        <ThreadListItem
          thread={archivedThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const titleElement = screen.getByText('How to play Catan?');
      expect(titleElement).toHaveClass('opacity-60');
    });

    it('shows active thread without reduced opacity', () => {
      render(
        <ThreadListItem
          thread={mockThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const titleElement = screen.getByText('How to play Catan?');
      expect(titleElement).not.toHaveClass('opacity-60');
    });
  });

  describe('Edge Cases', () => {
    it('handles thread with empty title gracefully', () => {
      const emptyTitleThread = { ...mockThread, title: '' };
      render(
        <ThreadListItem
          thread={emptyTitleThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('New Chat')).toBeInTheDocument();
    });

    it('handles thread with zero messages', () => {
      const noMessagesThread = { ...mockThread, messageCount: 0 };
      render(
        <ThreadListItem
          thread={noMessagesThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      expect(screen.getByText('0')).toBeInTheDocument();
    });

    it('truncates very long thread titles', () => {
      const longTitleThread = {
        ...mockThread,
        title: 'This is a very long thread title that should be truncated in the UI to prevent layout issues',
      };
      render(
        <ThreadListItem
          thread={longTitleThread}
          isActive={false}
          {...mockHandlers}
        />
      );

      const titleElement = screen.getByText(longTitleThread.title);
      expect(titleElement).toHaveClass('truncate');
    });
  });
});
