/**
 * MessageActions Component Tests (Issue #2245)
 *
 * Ensures React.memo optimization is correctly applied and component behavior is preserved.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { MessageActions } from '../MessageActions';
import { Message } from '@/types';
import { FEEDBACK_OUTCOMES } from '@/lib/constants/feedback';

// Mock message data
const mockUserMessage: Message = {
  id: '1',
  content: 'Test message',
  role: 'user',
  timestamp: new Date().toISOString(),
  chatThreadId: 'thread-1',
  feedback: null,
  isDeleted: false,
};

const mockAssistantMessage: Message = {
  id: '2',
  content: 'AI response',
  role: 'assistant',
  timestamp: new Date().toISOString(),
  chatThreadId: 'thread-1',
  feedback: null,
  isDeleted: false,
};

describe('MessageActions', () => {
  describe('User Message Actions', () => {
    it('should render edit and delete buttons for user messages', () => {
      render(
        <MessageActions
          message={mockUserMessage}
          isUser={true}
          onEdit={vi.fn()}
          onDelete={vi.fn()}
        />
      );

      expect(screen.getByLabelText('Edit message')).toBeInTheDocument();
      expect(screen.getByLabelText('Delete message')).toBeInTheDocument();
    });

    it('should call onEdit with message id and content when edit clicked', async () => {
      const onEdit = vi.fn();
      const user = userEvent.setup();

      render(
        <MessageActions
          message={mockUserMessage}
          isUser={true}
          onEdit={onEdit}
          onDelete={vi.fn()}
        />
      );

      await user.click(screen.getByLabelText('Edit message'));
      expect(onEdit).toHaveBeenCalledWith(mockUserMessage.id, mockUserMessage.content);
    });

    it('should call onDelete with message id when delete clicked', async () => {
      const onDelete = vi.fn();
      const user = userEvent.setup();

      render(
        <MessageActions
          message={mockUserMessage}
          isUser={true}
          onEdit={vi.fn()}
          onDelete={onDelete}
        />
      );

      await user.click(screen.getByLabelText('Delete message'));
      expect(onDelete).toHaveBeenCalledWith(mockUserMessage.id);
    });

    it('should disable buttons when isUpdating is true', () => {
      render(
        <MessageActions
          message={mockUserMessage}
          isUser={true}
          onEdit={vi.fn()}
          onDelete={vi.fn()}
          isUpdating={true}
        />
      );

      expect(screen.getByLabelText('Edit message')).toBeDisabled();
      expect(screen.getByLabelText('Delete message')).toBeDisabled();
    });

    it('should not render when isEditing is true', () => {
      const { container } = render(
        <MessageActions
          message={mockUserMessage}
          isUser={true}
          onEdit={vi.fn()}
          onDelete={vi.fn()}
          isEditing={true}
        />
      );

      expect(container.firstChild).toBeNull();
    });
  });

  describe('Assistant Message Actions', () => {
    it('should render helpful and not helpful buttons for assistant messages', () => {
      render(<MessageActions message={mockAssistantMessage} isUser={false} onFeedback={vi.fn()} />);

      expect(screen.getByLabelText('Mark as helpful')).toBeInTheDocument();
      expect(screen.getByLabelText('Mark as not helpful')).toBeInTheDocument();
    });

    it('should call onFeedback with HELPFUL when thumbs up clicked', async () => {
      const onFeedback = vi.fn();
      const user = userEvent.setup();

      render(
        <MessageActions message={mockAssistantMessage} isUser={false} onFeedback={onFeedback} />
      );

      await user.click(screen.getByLabelText('Mark as helpful'));
      expect(onFeedback).toHaveBeenCalledWith(mockAssistantMessage.id, FEEDBACK_OUTCOMES.HELPFUL);
    });

    it('should call onFeedback with NOT_HELPFUL when thumbs down clicked', async () => {
      const onFeedback = vi.fn();
      const user = userEvent.setup();

      render(
        <MessageActions message={mockAssistantMessage} isUser={false} onFeedback={onFeedback} />
      );

      await user.click(screen.getByLabelText('Mark as not helpful'));
      expect(onFeedback).toHaveBeenCalledWith(
        mockAssistantMessage.id,
        FEEDBACK_OUTCOMES.NOT_HELPFUL
      );
    });

    it('should show active state for helpful feedback', () => {
      const messageWithFeedback = {
        ...mockAssistantMessage,
        feedback: FEEDBACK_OUTCOMES.HELPFUL,
      };

      render(<MessageActions message={messageWithFeedback} isUser={false} onFeedback={vi.fn()} />);

      const helpfulButton = screen.getByLabelText('Mark as helpful');
      expect(helpfulButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('should show active state for not helpful feedback', () => {
      const messageWithFeedback = {
        ...mockAssistantMessage,
        feedback: FEEDBACK_OUTCOMES.NOT_HELPFUL,
      };

      render(<MessageActions message={messageWithFeedback} isUser={false} onFeedback={vi.fn()} />);

      const notHelpfulButton = screen.getByLabelText('Mark as not helpful');
      expect(notHelpfulButton).toHaveAttribute('aria-pressed', 'true');
    });
  });

  describe('React.memo Optimization (Issue #2245)', () => {
    it('should be wrapped with React.memo', () => {
      // Verify component is defined and memoized
      expect(MessageActions).toBeDefined();

      // React.memo wraps the component, making it an object type
      expect(typeof MessageActions).toBe('object');

      // Note: React.memo effectiveness verified through actual usage
      // Parent (Message.tsx) passes stable callbacks from Zustand store:
      // - useChatStore: startEditMessage, deleteMessage, setMessageFeedback
      // These Zustand methods are stable references, so memo prevents re-renders.
    });
  });

  describe('Edge Cases', () => {
    it('should return null when isUser is true and isEditing is true', () => {
      const { container } = render(
        <MessageActions
          message={mockUserMessage}
          isUser={true}
          isEditing={true}
          onEdit={vi.fn()}
          onDelete={vi.fn()}
        />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should handle missing callback props gracefully', () => {
      render(<MessageActions message={mockUserMessage} isUser={true} />);

      // Should render without callbacks (buttons exist but do nothing)
      expect(screen.getByLabelText('Edit message')).toBeInTheDocument();
    });

    it('should render feedback buttons even when onFeedback is undefined', () => {
      render(<MessageActions message={mockAssistantMessage} isUser={false} />);

      // Feedback buttons render regardless of onFeedback prop
      expect(screen.getByLabelText('Mark as helpful')).toBeInTheDocument();
      expect(screen.getByLabelText('Mark as not helpful')).toBeInTheDocument();
    });
  });
});
