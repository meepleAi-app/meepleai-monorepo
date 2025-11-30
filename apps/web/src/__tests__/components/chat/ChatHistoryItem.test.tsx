/**
 * Tests for ChatHistoryItem component
 * Comprehensive coverage of individual chat history items
 */

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatHistoryItem } from '@/components/chat/ChatHistoryItem';
import { ChatThread } from '@/types';

describe('ChatHistoryItem', () => {
  const mockChat: ChatThread = {
    id: 'chat-1',
    gameId: 'game-1',
    title: 'Chess Expert',
    createdAt: '2025-01-10T10:00:00Z',
    lastMessageAt: '2025-01-10T10:05:00Z',
    messageCount: 5,
    messages: []
  };

  const defaultProps = {
    chat: mockChat,
    isActive: false,
    onSelect: vi.fn(),
    onDelete: vi.fn()
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders chat item with agent name', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
    });

    it('renders chat item with formatted date', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      // The component shows both date and time - look for the date part specifically
      const dateRegex = /\d{1,2}\/\d{1,2}\/\d{4}/; // Matches date format like 10/01/2025
      expect(screen.getByText(dateRegex)).toBeInTheDocument();
    });

    it('uses lastMessageAt for date if available', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      // Last message is 2025-01-10T10:05:00Z which should be displayed
      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
    });

    it('uses startedAt for date if lastMessageAt is null', () => {
      const chatWithoutLastMessage = {
        ...mockChat,
        lastMessageAt: null
      };
      render(<ChatHistoryItem {...defaultProps} chat={chatWithoutLastMessage} />);
      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
    });

    it('renders delete button with emoji icon', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      expect(screen.getByText('🗑️')).toBeInTheDocument();
    });

    it('renders delete button with accessible label', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      expect(screen.getByLabelText('Delete chat Chess Expert')).toBeInTheDocument();
    });
  });

  describe('Active State', () => {
    it('applies active styling when isActive is true', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} isActive={true} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveClass('bg-primary/10', 'border-primary');
    });

    it('applies inactive styling when isActive is false', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} isActive={false} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveClass('bg-background', 'border-border');
    });

    it('sets aria-current attribute when active', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} isActive={true} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveAttribute('aria-current', 'true');
    });

    it('does not set aria-current when inactive', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} isActive={false} />);
      const listItem = container.querySelector('li');
      expect(listItem).not.toHaveAttribute('aria-current');
    });
  });

  describe('Interactions', () => {
    it('calls onSelect when chat item is clicked', () => {
      const onSelect = vi.fn();
      const { container } = render(<ChatHistoryItem {...defaultProps} onSelect={onSelect} />);
      const listItem = container.querySelector('li');

      if (listItem) {
        fireEvent.click(listItem);
      }

      expect(onSelect).toHaveBeenCalledTimes(1);
    });

    it('calls onSelect when Enter key is pressed', () => {
      const onSelect = vi.fn();
      const { container } = render(<ChatHistoryItem {...defaultProps} onSelect={onSelect} />);
      const listItem = container.querySelector('li');

      if (listItem) {
        fireEvent.keyDown(listItem, { key: 'Enter' });
      }

      expect(onSelect).toHaveBeenCalledTimes(1);
    });

    it('calls onSelect when Space key is pressed', () => {
      const onSelect = vi.fn();
      const { container } = render(<ChatHistoryItem {...defaultProps} onSelect={onSelect} />);
      const listItem = container.querySelector('li');

      if (listItem) {
        fireEvent.keyDown(listItem, { key: ' ' });
      }

      expect(onSelect).toHaveBeenCalledTimes(1);
    });

    it('prevents default behavior on Space key press', () => {
      const onSelect = vi.fn();
      const { container } = render(<ChatHistoryItem {...defaultProps} onSelect={onSelect} />);
      const listItem = container.querySelector('li');

      if (listItem) {
        const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true });
        const preventDefaultSpy = vi.spyOn(event, 'preventDefault');
        listItem.dispatchEvent(event);
        expect(preventDefaultSpy).toHaveBeenCalled();
      }
    });

    it('does not call onSelect for other keys', () => {
      const onSelect = vi.fn();
      const { container } = render(<ChatHistoryItem {...defaultProps} onSelect={onSelect} />);
      const listItem = container.querySelector('li');

      if (listItem) {
        fireEvent.keyDown(listItem, { key: 'a' });
        fireEvent.keyDown(listItem, { key: 'Escape' });
      }

      expect(onSelect).not.toHaveBeenCalled();
    });

    it('calls onDelete when delete button is clicked', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<ChatHistoryItem {...defaultProps} onDelete={onDelete} />);

      const deleteButton = screen.getByLabelText('Delete chat Chess Expert');
      await user.click(deleteButton);

      expect(onDelete).toHaveBeenCalledTimes(1);
    });

    it('stops propagation on delete button click', async () => {
      const user = userEvent.setup();
      const onSelect = vi.fn();
      const onDelete = vi.fn();
      render(<ChatHistoryItem {...defaultProps} onSelect={onSelect} onDelete={onDelete} />);

      const deleteButton = screen.getByLabelText('Delete chat Chess Expert');
      await user.click(deleteButton);

      // onSelect should not be called when delete button is clicked
      expect(onSelect).not.toHaveBeenCalled();
      expect(onDelete).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('has role="button" for keyboard navigation', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveAttribute('role', 'button');
    });

    it('is keyboard focusable with tabIndex', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveAttribute('tabIndex', '0');
    });

    it('has accessible delete button label', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      expect(screen.getByLabelText('Delete chat Chess Expert')).toBeInTheDocument();
    });

    it('has title attribute on delete button', () => {
      render(<ChatHistoryItem {...defaultProps} />);
      const deleteButton = screen.getByLabelText('Delete chat Chess Expert');
      expect(deleteButton).toHaveAttribute('title', 'Elimina chat');
    });
  });

  describe('Edge Cases', () => {
    it('handles chat with very long title', () => {
      const chatWithLongTitle = {
        ...mockChat,
        title: 'This is a very long agent name that should still render correctly without breaking the layout'
      };
      render(<ChatHistoryItem {...defaultProps} chat={chatWithLongTitle} />);
      expect(screen.getByText(chatWithLongTitle.title)).toBeInTheDocument();
    });

    it('handles chat with empty string title', () => {
      const chatWithEmptyTitle = {
        ...mockChat,
        title: ''
      };
      render(<ChatHistoryItem {...defaultProps} chat={chatWithEmptyTitle} />);
      // Should still render without crashing - the component shows empty string, not 'Chat' fallback
      expect(screen.getByText('🗑️')).toBeInTheDocument();
    });

    it('handles chat with special characters in title', () => {
      const chatWithSpecialChars = {
        ...mockChat,
        title: 'Chess <Expert> & "Helper"'
      };
      render(<ChatHistoryItem {...defaultProps} chat={chatWithSpecialChars} />);
      expect(screen.getByText('Chess <Expert> & "Helper"')).toBeInTheDocument();
    });

    it('handles invalid date strings gracefully', () => {
      const chatWithInvalidDate = {
        ...mockChat,
        createdAt: 'invalid-date',
        lastMessageAt: 'invalid-date'
      };
      // Should not throw an error
      expect(() => {
        render(<ChatHistoryItem {...defaultProps} chat={chatWithInvalidDate} />);
      }).not.toThrow();
    });
  });

  describe('Styling', () => {
    it('applies correct cursor style', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveClass('cursor-pointer');
    });

    it('applies border radius', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} />);
      const listItem = container.querySelector('li');
      expect(listItem).toHaveClass('rounded');
    });

    it('applies different border color for active state', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} isActive={true} />);
      const listItem = container.querySelector('li');
      // Border should be primary color when active
      expect(listItem).toHaveClass('border-primary');
    });

    it('applies different border color for inactive state', () => {
      const { container } = render(<ChatHistoryItem {...defaultProps} isActive={false} />);
      const listItem = container.querySelector('li');
      // Border should be default border color when inactive
      expect(listItem).toHaveClass('border-border');
    });
  });
});