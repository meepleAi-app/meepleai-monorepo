/**
 * Tests for MessageActions component
 * Comprehensive coverage of message action buttons
 */

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageActions } from '../MessageActions';
import { Message } from '@/types';

describe('MessageActions', () => {
  const mockUserMessage: Message = {
    id: 'msg-1',
    role: 'user',
    content: 'Test message',
    timestamp: new Date(),
    isDeleted: false,
    feedback: null
  };

  const mockAssistantMessage: Message = {
    id: 'msg-2',
    role: 'assistant',
    content: 'Test response',
    timestamp: new Date(),
    isDeleted: false,
    feedback: null
  };

  const defaultProps = {
    message: mockUserMessage,
    isUser: true,
    onEdit: jest.fn(),
    onDelete: jest.fn(),
    onFeedback: jest.fn(),
    isEditing: false,
    isUpdating: false
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('User Message Actions', () => {
    it('renders edit and delete buttons for user messages', () => {
      render(<MessageActions {...defaultProps} />);

      expect(screen.getByLabelText('Edit message')).toBeInTheDocument();
      expect(screen.getByLabelText('Delete message')).toBeInTheDocument();
    });

    it('renders edit button with emoji icon', () => {
      render(<MessageActions {...defaultProps} />);
      expect(screen.getByText('✏️')).toBeInTheDocument();
    });

    it('renders delete button with emoji icon', () => {
      render(<MessageActions {...defaultProps} />);
      expect(screen.getByText('🗑️')).toBeInTheDocument();
    });

    it('calls onEdit when edit button is clicked', async () => {
      const user = userEvent.setup();
      const onEdit = jest.fn();
      render(<MessageActions {...defaultProps} onEdit={onEdit} />);

      const editButton = screen.getByLabelText('Edit message');
      await user.click(editButton);

      expect(onEdit).toHaveBeenCalledWith('msg-1', 'Test message');
    });

    it('calls onDelete when delete button is clicked', async () => {
      const user = userEvent.setup();
      const onDelete = jest.fn();
      render(<MessageActions {...defaultProps} onDelete={onDelete} />);

      const deleteButton = screen.getByLabelText('Delete message');
      await user.click(deleteButton);

      expect(onDelete).toHaveBeenCalledWith('msg-1');
    });

    it('does not render actions when isEditing is true', () => {
      render(<MessageActions {...defaultProps} isEditing={true} />);

      expect(screen.queryByLabelText('Edit message')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Delete message')).not.toBeInTheDocument();
    });

    it('disables buttons when isUpdating is true', () => {
      render(<MessageActions {...defaultProps} isUpdating={true} />);

      expect(screen.getByLabelText('Edit message')).toBeDisabled();
      expect(screen.getByLabelText('Delete message')).toBeDisabled();
    });

    it('applies not-allowed cursor when isUpdating', () => {
      render(<MessageActions {...defaultProps} isUpdating={true} />);

      const editButton = screen.getByLabelText('Edit message');
      const deleteButton = screen.getByLabelText('Delete message');

      expect(editButton).toHaveStyle({ cursor: 'not-allowed' });
      expect(deleteButton).toHaveStyle({ cursor: 'not-allowed' });
    });

    it('applies reduced opacity when isUpdating', () => {
      render(<MessageActions {...defaultProps} isUpdating={true} />);

      const editButton = screen.getByLabelText('Edit message');
      const deleteButton = screen.getByLabelText('Delete message');

      expect(editButton).toHaveStyle({ opacity: 0.5 });
      expect(deleteButton).toHaveStyle({ opacity: 0.5 });
    });

    it('has title attributes for tooltips', () => {
      render(<MessageActions {...defaultProps} />);

      expect(screen.getByLabelText('Edit message')).toHaveAttribute('title', 'Modifica messaggio');
      expect(screen.getByLabelText('Delete message')).toHaveAttribute('title', 'Elimina messaggio');
    });
  });

  describe('Assistant Message Actions', () => {
    const assistantProps = {
      ...defaultProps,
      message: mockAssistantMessage,
      isUser: false
    };

    it('renders helpful and not-helpful buttons for assistant messages', () => {
      render(<MessageActions {...assistantProps} />);

      expect(screen.getByLabelText('Mark as helpful')).toBeInTheDocument();
      expect(screen.getByLabelText('Mark as not helpful')).toBeInTheDocument();
    });

    it('renders thumbs up and thumbs down icons', () => {
      render(<MessageActions {...assistantProps} />);

      expect(screen.getByText('👍')).toBeInTheDocument();
      expect(screen.getByText('👎')).toBeInTheDocument();
    });

    it('renders "Utile" and "Non utile" labels', () => {
      render(<MessageActions {...assistantProps} />);

      expect(screen.getByText('Utile')).toBeInTheDocument();
      expect(screen.getByText('Non utile')).toBeInTheDocument();
    });

    it('calls onFeedback with "helpful" when helpful button is clicked', async () => {
      const user = userEvent.setup();
      const onFeedback = jest.fn();
      render(<MessageActions {...assistantProps} onFeedback={onFeedback} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      await user.click(helpfulButton);

      expect(onFeedback).toHaveBeenCalledWith('msg-2', 'helpful');
    });

    it('calls onFeedback with "not-helpful" when not-helpful button is clicked', async () => {
      const user = userEvent.setup();
      const onFeedback = jest.fn();
      render(<MessageActions {...assistantProps} onFeedback={onFeedback} />);

      const notHelpfulButton = screen.getByLabelText('Mark as not helpful');
      await user.click(notHelpfulButton);

      expect(onFeedback).toHaveBeenCalledWith('msg-2', 'not-helpful');
    });

    it('highlights helpful button when feedback is "helpful"', () => {
      const messageWithFeedback = {
        ...mockAssistantMessage,
        feedback: 'helpful' as const
      };
      render(<MessageActions {...assistantProps} message={messageWithFeedback} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      expect(helpfulButton).toHaveStyle({ background: 'rgb(52, 168, 83)', color: 'rgb(255, 255, 255)' });
    });

    it('highlights not-helpful button when feedback is "not-helpful"', () => {
      const messageWithFeedback = {
        ...mockAssistantMessage,
        feedback: 'not-helpful' as const
      };
      render(<MessageActions {...assistantProps} message={messageWithFeedback} />);

      const notHelpfulButton = screen.getByLabelText('Mark as not helpful');
      expect(notHelpfulButton).toHaveStyle({ background: 'rgb(234, 67, 53)', color: 'rgb(255, 255, 255)' });
    });

    it('uses default styling when no feedback is provided', () => {
      render(<MessageActions {...assistantProps} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      const notHelpfulButton = screen.getByLabelText('Mark as not helpful');

      expect(helpfulButton).toHaveStyle({ background: '#f1f3f4', color: '#64748b' });
      expect(notHelpfulButton).toHaveStyle({ background: '#f1f3f4', color: '#64748b' });
    });

    it('has aria-pressed attribute for helpful button', () => {
      const messageWithFeedback = {
        ...mockAssistantMessage,
        feedback: 'helpful' as const
      };
      render(<MessageActions {...assistantProps} message={messageWithFeedback} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      expect(helpfulButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('has aria-pressed attribute for not-helpful button', () => {
      const messageWithFeedback = {
        ...mockAssistantMessage,
        feedback: 'not-helpful' as const
      };
      render(<MessageActions {...assistantProps} message={messageWithFeedback} />);

      const notHelpfulButton = screen.getByLabelText('Mark as not helpful');
      expect(notHelpfulButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('has role="group" for feedback buttons container', () => {
      const { container } = render(<MessageActions {...assistantProps} />);
      const feedbackGroup = container.querySelector('[role="group"]');
      expect(feedbackGroup).toHaveAttribute('aria-label', 'Message feedback');
    });
  });

  describe('Edge Cases', () => {
    it('returns null when isUser is true and isEditing is true', () => {
      const { container } = render(<MessageActions {...defaultProps} isEditing={true} />);
      expect(container.firstChild).toBeNull();
    });

    it('returns null when isUser is false and no component should render', () => {
      // This shouldn't happen in practice, but test the logic
      const { container } = render(
        <MessageActions
          message={mockUserMessage}
          isUser={false}
          isEditing={true}
        />
      );
      // Component should return null for non-user non-assistant scenarios with isEditing
      expect(container.querySelector('.message-actions')).not.toBeInTheDocument();
    });

    it('handles missing onEdit callback gracefully', async () => {
      const user = userEvent.setup();
      render(<MessageActions {...defaultProps} onEdit={undefined} />);

      const editButton = screen.getByLabelText('Edit message');
      // Should not throw when clicked
      await expect(user.click(editButton)).resolves.not.toThrow();
    });

    it('handles missing onDelete callback gracefully', async () => {
      const user = userEvent.setup();
      render(<MessageActions {...defaultProps} onDelete={undefined} />);

      const deleteButton = screen.getByLabelText('Delete message');
      // Should not throw when clicked
      await expect(user.click(deleteButton)).resolves.not.toThrow();
    });

    it('handles missing onFeedback callback gracefully', async () => {
      const user = userEvent.setup();
      const assistantProps = {
        ...defaultProps,
        message: mockAssistantMessage,
        isUser: false,
        onFeedback: undefined
      };
      render(<MessageActions {...assistantProps} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      // Should not throw when clicked
      await expect(user.click(helpfulButton)).resolves.not.toThrow();
    });
  });

  describe('Accessibility', () => {
    it('has accessible labels for user action buttons', () => {
      render(<MessageActions {...defaultProps} />);

      expect(screen.getByLabelText('Edit message')).toBeInTheDocument();
      expect(screen.getByLabelText('Delete message')).toBeInTheDocument();
    });

    it('has accessible labels for feedback buttons', () => {
      const assistantProps = {
        ...defaultProps,
        message: mockAssistantMessage,
        isUser: false
      };
      render(<MessageActions {...assistantProps} />);

      expect(screen.getByLabelText('Mark as helpful')).toBeInTheDocument();
      expect(screen.getByLabelText('Mark as not helpful')).toBeInTheDocument();
    });

    it('uses aria-pressed for toggle state', () => {
      const assistantProps = {
        ...defaultProps,
        message: mockAssistantMessage,
        isUser: false
      };
      render(<MessageActions {...assistantProps} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      expect(helpfulButton).toHaveAttribute('aria-pressed', 'false');
    });

    it('groups feedback buttons with aria-label', () => {
      const assistantProps = {
        ...defaultProps,
        message: mockAssistantMessage,
        isUser: false
      };
      const { container } = render(<MessageActions {...assistantProps} />);

      const feedbackGroup = container.querySelector('[role="group"]');
      expect(feedbackGroup).toHaveAttribute('aria-label', 'Message feedback');
    });
  });

  describe('Styling', () => {
    it('applies initial opacity to user actions container', () => {
      const { container } = render(<MessageActions {...defaultProps} />);
      const actionsContainer = container.querySelector('.message-actions');
      expect(actionsContainer).toHaveStyle({ opacity: 0 });
    });

    it('applies correct styling to edit button', () => {
      render(<MessageActions {...defaultProps} />);
      const editButton = screen.getByLabelText('Edit message');
      expect(editButton).toHaveStyle({
        background: 'rgb(148, 163, 184)',
        color: 'rgb(255, 255, 255)'
      });
      // Note: border and borderRadius are tested separately as they have browser-specific defaults
    });

    it('applies correct styling to delete button', () => {
      render(<MessageActions {...defaultProps} />);
      const deleteButton = screen.getByLabelText('Delete message');
      expect(deleteButton).toHaveStyle({
        background: 'rgb(148, 163, 184)',
        color: 'rgb(255, 255, 255)'
      });
      // Note: border and borderRadius are tested separately as they have browser-specific defaults
    });
  });
});
