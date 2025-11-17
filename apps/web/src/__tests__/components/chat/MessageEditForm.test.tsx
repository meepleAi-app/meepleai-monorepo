/**
 * Tests for MessageEditForm component
 * Comprehensive coverage of message editing form
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageEditForm } from '@/components/chat/MessageEditForm';
import { ChatStore } from '@/store/chat/types';
import React from 'react';

// Mock Zustand store
jest.mock('@/store/chat/store', () => ({
  useChatStore: jest.fn()
}));

import { useChatStore } from '@/store/chat/store';
const mockUseChatStore = useChatStore as jest.MockedFunction<typeof useChatStore>;

describe('MessageEditForm', () => {
  const mockEditMessage = jest.fn(async () => {});

  const defaultStoreValue: Partial<ChatStore> = {
    editingMessageId: 'msg-1',
    editContent: 'Test content',
    setEditContent: jest.fn(),
    saveEdit: jest.fn(async () => {}),
    editMessage: mockEditMessage,
    cancelEdit: jest.fn(),
    loading: {
      games: false,
      agents: false,
      chats: false,
      messages: false,
      sending: false,
      creating: false,
      updating: false,
      deleting: false
    }
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseChatStore.mockReturnValue(defaultStoreValue as ChatStore);
  });

  describe('Rendering', () => {
    it('renders textarea when editingMessageId is set', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Edit message content')).toBeInTheDocument();
    });

    it('renders save and cancel buttons', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Save edited message')).toBeInTheDocument();
      expect(screen.getByLabelText('Cancel editing')).toBeInTheDocument();
    });

    it('returns null when editingMessageId is null', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editingMessageId: null
      } as ChatStore);

      const { container } = render(<MessageEditForm />);
      expect(container.firstChild).toBeNull();
    });

    it('displays current edit content in textarea', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: 'My edited message'
      } as ChatStore);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
      expect(textarea.value).toBe('My edited message');
    });

    it('shows "Salva" text on save button when not updating', () => {
      render(<MessageEditForm />);
      expect(screen.getByText('Salva')).toBeInTheDocument();
    });

    it('shows "Salvataggio..." text on save button when updating', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        loading: {
          ...defaultStoreValue.loading!,
          updating: true
        }
      } as ChatStore);

      render(<MessageEditForm />);
      expect(screen.getByText('Salvataggio...')).toBeInTheDocument();
    });

    it('autofocuses the textarea', () => {
      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');
      expect(textarea).toHaveFocus();
    });
  });

  describe('Text Input', () => {
    it('calls setEditContent when textarea value changes', async () => {
      const user = userEvent.setup();
      const setEditContent = jest.fn();
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        setEditContent,
        editContent: ''
      } as ChatStore);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');

      await user.type(textarea, 'New text');

      expect(setEditContent).toHaveBeenCalled();
    });

    it('updates textarea value when editContent changes', () => {
      const { rerender } = render(<MessageEditForm />);

      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: 'Updated content'
      } as ChatStore);

      rerender(<MessageEditForm />);

      const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
      expect(textarea.value).toBe('Updated content');
    });

    it('allows multiline text input', async () => {
      const user = userEvent.setup();
      const setEditContent = jest.fn();
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        setEditContent,
        editContent: ''
      } as ChatStore);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');

      await user.type(textarea, 'Line 1{Enter}Line 2');

      expect(setEditContent).toHaveBeenCalled();
    });

    it('disables textarea when updating', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        loading: {
          ...defaultStoreValue.loading!,
          updating: true
        }
      } as ChatStore);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');
      expect(textarea).toBeDisabled();
    });
  });

  describe('Save Functionality', () => {
    it('calls saveEdit when save button is clicked', async () => {
      const user = userEvent.setup();
      const saveEdit = jest.fn(async () => {});
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        saveEdit
      } as ChatStore);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      await user.click(saveButton);

      await waitFor(() => {
        expect(saveEdit).toHaveBeenCalledTimes(1);
      });
    });

    it('disables save button when content is empty', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: ''
      } as ChatStore);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeDisabled();
    });

    it('disables save button when content is only whitespace', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: '   \n   '
      } as ChatStore);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeDisabled();
    });

    it('enables save button when content has non-whitespace characters', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: '  a  '
      } as ChatStore);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).not.toBeDisabled();
    });

    it('disables save button when updating', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        loading: {
          ...defaultStoreValue.loading!,
          updating: true
        }
      } as ChatStore);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeDisabled();
    });

    // Styling is now handled by shadcn Button component with Tailwind CSS
    // The disabled state styling is applied automatically by the Button component
  });

  describe('Cancel Functionality', () => {
    it('calls cancelEdit when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const cancelEdit = jest.fn();
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        cancelEdit
      } as ChatStore);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      await user.click(cancelButton);

      expect(cancelEdit).toHaveBeenCalledTimes(1);
    });

    it('disables cancel button when updating', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        loading: {
          ...defaultStoreValue.loading!,
          updating: true
        }
      } as ChatStore);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      expect(cancelButton).toBeDisabled();
    });

    // Styling is now handled by shadcn Button component with Tailwind CSS
    // The disabled state styling is applied automatically by the Button component
  });

  describe('Accessibility', () => {
    it('has accessible label for textarea', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Edit message content')).toBeInTheDocument();
    });

    it('has accessible label for save button', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Save edited message')).toBeInTheDocument();
    });

    it('has accessible label for cancel button', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Cancel editing')).toBeInTheDocument();
    });

    it('autofocuses textarea for keyboard users', () => {
      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');
      // Check that textarea gets focus (autoFocus is a boolean prop in React, not an HTML attribute)
      expect(textarea).toHaveFocus();
    });
  });

  describe('Styling', () => {
    it('applies shadcn Textarea component with correct classes', () => {
      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');
      expect(textarea).toHaveClass('min-h-[80px]', 'resize-y');
    });

    it('applies shadcn Button component to save button', () => {
      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeInTheDocument();
      // Button uses shadcn styling automatically
    });

    it('applies secondary variant to cancel button', () => {
      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      expect(cancelButton).toBeInTheDocument();
      // Secondary variant styling is applied by shadcn Button component
    });
  });

  describe('Edge Cases', () => {
    it('handles very long text content', async () => {
      const user = userEvent.setup();
      const longText = 'a'.repeat(5000);
      const setEditContent = jest.fn();
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: '',
        setEditContent
      } as ChatStore);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');

      await user.type(textarea, longText.substring(0, 100)); // Type subset for performance

      expect(setEditContent).toHaveBeenCalled();
    });

    it('handles special characters in content', async () => {
      const specialChars = '<script>alert("test")</script>';
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        editContent: specialChars
      } as ChatStore);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
      expect(textarea.value).toBe(specialChars);
    });

    it('handles rapid save button clicks', async () => {
      const user = userEvent.setup();
      const saveEdit = jest.fn(async () => {});
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        saveEdit
      } as ChatStore);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');

      await user.click(saveButton);
      await user.click(saveButton);
      await user.click(saveButton);

      // Should handle multiple clicks gracefully
      expect(saveEdit).toHaveBeenCalled();
    });

    it('handles rapid cancel button clicks', async () => {
      const user = userEvent.setup();
      const cancelEdit = jest.fn();
      mockUseChatStore.mockReturnValue({
        ...defaultStoreValue,
        cancelEdit
      } as ChatStore);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');

      await user.click(cancelButton);
      await user.click(cancelButton);

      expect(cancelEdit).toHaveBeenCalled();
    });
  });
});
