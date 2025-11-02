/**
 * Tests for MessageEditForm component
 * Comprehensive coverage of message editing form
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageEditForm } from '../MessageEditForm';
import { ChatContextValue } from '../ChatProvider';
import React from 'react';

// Mock ChatProvider context
jest.mock('../ChatProvider', () => ({
  useChatContext: jest.fn()
}));

import { useChatContext } from '../ChatProvider';
const mockUseChatContext = useChatContext as jest.MockedFunction<typeof useChatContext>;

describe('MessageEditForm', () => {
  const defaultContextValue: Partial<ChatContextValue> = {
    editingMessageId: 'msg-1',
    editContent: 'Test content',
    setEditContent: jest.fn(),
    saveEdit: jest.fn(async () => {}),
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
    mockUseChatContext.mockReturnValue(defaultContextValue as ChatContextValue);
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
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editingMessageId: null
      } as ChatContextValue);

      const { container } = render(<MessageEditForm />);
      expect(container.firstChild).toBeNull();
    });

    it('displays current edit content in textarea', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: 'My edited message'
      } as ChatContextValue);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
      expect(textarea.value).toBe('My edited message');
    });

    it('shows "Salva" text on save button when not updating', () => {
      render(<MessageEditForm />);
      expect(screen.getByText('Salva')).toBeInTheDocument();
    });

    it('shows "Salvataggio..." text on save button when updating', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        loading: {
          ...defaultContextValue.loading!,
          updating: true
        }
      } as ChatContextValue);

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
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        setEditContent,
        editContent: ''
      } as ChatContextValue);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');

      await user.type(textarea, 'New text');

      expect(setEditContent).toHaveBeenCalled();
    });

    it('updates textarea value when editContent changes', () => {
      const { rerender } = render(<MessageEditForm />);

      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: 'Updated content'
      } as ChatContextValue);

      rerender(<MessageEditForm />);

      const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
      expect(textarea.value).toBe('Updated content');
    });

    it('allows multiline text input', async () => {
      const user = userEvent.setup();
      const setEditContent = jest.fn();
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        setEditContent,
        editContent: ''
      } as ChatContextValue);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');

      await user.type(textarea, 'Line 1{Enter}Line 2');

      expect(setEditContent).toHaveBeenCalled();
    });

    it('disables textarea when updating', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        loading: {
          ...defaultContextValue.loading!,
          updating: true
        }
      } as ChatContextValue);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');
      expect(textarea).toBeDisabled();
    });
  });

  describe('Save Functionality', () => {
    it('calls saveEdit when save button is clicked', async () => {
      const user = userEvent.setup();
      const saveEdit = jest.fn(async () => {});
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        saveEdit
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      await user.click(saveButton);

      await waitFor(() => {
        expect(saveEdit).toHaveBeenCalledTimes(1);
      });
    });

    it('disables save button when content is empty', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: ''
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeDisabled();
    });

    it('disables save button when content is only whitespace', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: '   \n   '
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeDisabled();
    });

    it('enables save button when content has non-whitespace characters', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: '  a  '
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).not.toBeDisabled();
    });

    it('disables save button when updating', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        loading: {
          ...defaultContextValue.loading!,
          updating: true
        }
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeDisabled();
    });

    it('applies not-allowed cursor when save button is disabled', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: ''
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toHaveStyle({ cursor: 'not-allowed' });
    });

    it('applies pointer cursor when save button is enabled', () => {
      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toHaveStyle({ cursor: 'pointer' });
    });

    it('changes save button color when disabled', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: ''
      } as ChatContextValue);

      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toHaveStyle({ background: '#cbd5e1' });
    });

    it('uses primary color for enabled save button', () => {
      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toHaveStyle({ background: '#1a73e8' });
    });
  });

  describe('Cancel Functionality', () => {
    it('calls cancelEdit when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const cancelEdit = jest.fn();
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        cancelEdit
      } as ChatContextValue);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      await user.click(cancelButton);

      expect(cancelEdit).toHaveBeenCalledTimes(1);
    });

    it('disables cancel button when updating', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        loading: {
          ...defaultContextValue.loading!,
          updating: true
        }
      } as ChatContextValue);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      expect(cancelButton).toBeDisabled();
    });

    it('applies not-allowed cursor when cancel button is disabled', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        loading: {
          ...defaultContextValue.loading!,
          updating: true
        }
      } as ChatContextValue);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      expect(cancelButton).toHaveStyle({ cursor: 'not-allowed' });
    });

    it('reduces opacity of cancel button when updating', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        loading: {
          ...defaultContextValue.loading!,
          updating: true
        }
      } as ChatContextValue);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      expect(cancelButton).toHaveStyle({ opacity: 0.5 });
    });
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
    it('applies correct styling to textarea', () => {
      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');
      expect(textarea).toHaveStyle({
        width: '100%',
        minHeight: '80px',
        padding: '8px',
        fontSize: '14px',
        borderRadius: '4px'
      });
    });

    it('applies correct styling to save button', () => {
      render(<MessageEditForm />);
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toHaveStyle({
        padding: '6px 12px',
        borderRadius: '4px',
        fontSize: '12px',
        fontWeight: '500'
      });
    });

    it('applies correct styling to cancel button', () => {
      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');
      expect(cancelButton).toHaveStyle({
        padding: '6px 12px',
        background: '#94a3b8',
        borderRadius: '4px',
        fontSize: '12px',
        fontWeight: '500'
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles very long text content', async () => {
      const user = userEvent.setup();
      const longText = 'a'.repeat(5000);
      const setEditContent = jest.fn();
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: '',
        setEditContent
      } as ChatContextValue);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content');

      await user.type(textarea, longText.substring(0, 100)); // Type subset for performance

      expect(setEditContent).toHaveBeenCalled();
    });

    it('handles special characters in content', async () => {
      const specialChars = '<script>alert("test")</script>';
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        editContent: specialChars
      } as ChatContextValue);

      render(<MessageEditForm />);
      const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
      expect(textarea.value).toBe(specialChars);
    });

    it('handles rapid save button clicks', async () => {
      const user = userEvent.setup();
      const saveEdit = jest.fn(async () => {});
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        saveEdit
      } as ChatContextValue);

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
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        cancelEdit
      } as ChatContextValue);

      render(<MessageEditForm />);
      const cancelButton = screen.getByLabelText('Cancel editing');

      await user.click(cancelButton);
      await user.click(cancelButton);

      expect(cancelEdit).toHaveBeenCalled();
    });
  });
});
