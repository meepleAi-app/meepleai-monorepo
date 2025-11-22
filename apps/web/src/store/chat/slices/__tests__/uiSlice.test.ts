/**
 * Test Suite: uiSlice.ts (Issue #1083)
 *
 * Comprehensive tests for UI state management:
 * - Loading states (all 8 flags)
 * - Error state management
 * - Input value management
 * - Message editing state
 * - Search mode toggle
 * - All setter actions
 *
 * Target Coverage: 90%+
 */

import { renderHook, act } from '@testing-library/react';
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createUISlice } from '../uiSlice';
import { UISlice } from '../../types';

// ============================================================================
// Test Store Setup
// ============================================================================

/**
 * Create isolated test store with UI slice only
 * Mimics production middleware stack without persistence/devtools
 */
const createTestStore = () => {
  return create<UISlice>()(
    subscribeWithSelector(
      immer((set, get, store) => ({
        ...createUISlice(set as any, get as any, store as any),
      }))
    )
  );
};

// ============================================================================
// Initial State Tests
// ============================================================================

describe('uiSlice - Initial State', () => {
  it('should have all loading flags set to false', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    expect(result.current.loading).toEqual({
      chats: false,
      messages: false,
      sending: false,
      creating: false,
      updating: false,
      deleting: false,
      games: false,
      agents: false,
    });
  });

  it('should have null error initially', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    expect(result.current.error).toBeNull();
  });

  it('should have empty inputValue initially', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    expect(result.current.inputValue).toBe('');
  });

  it('should have null editingMessageId initially', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    expect(result.current.editingMessageId).toBeNull();
  });

  it('should have empty editContent initially', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    expect(result.current.editContent).toBe('');
  });

  it('should have Hybrid searchMode initially', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    expect(result.current.searchMode).toBe('Hybrid');
  });
});

// ============================================================================
// Loading State Tests - All 8 Flags
// ============================================================================

describe('uiSlice - Loading States', () => {
  describe('setLoading action', () => {
    it('should set chats loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('chats', true);
      });

      expect(result.current.loading.chats).toBe(true);
      // Other flags should remain unchanged
      expect(result.current.loading.messages).toBe(false);
    });

    it('should set messages loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('messages', true);
      });

      expect(result.current.loading.messages).toBe(true);
    });

    it('should set sending loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('sending', true);
      });

      expect(result.current.loading.sending).toBe(true);
    });

    it('should set creating loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('creating', true);
      });

      expect(result.current.loading.creating).toBe(true);
    });

    it('should set updating loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('updating', true);
      });

      expect(result.current.loading.updating).toBe(true);
    });

    it('should set deleting loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('deleting', true);
      });

      expect(result.current.loading.deleting).toBe(true);
    });

    it('should set games loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('games', true);
      });

      expect(result.current.loading.games).toBe(true);
    });

    it('should set agents loading flag to true', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('agents', true);
      });

      expect(result.current.loading.agents).toBe(true);
    });

    it('should toggle loading flag from true to false', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('sending', true);
      });
      expect(result.current.loading.sending).toBe(true);

      act(() => {
        result.current.setLoading('sending', false);
      });
      expect(result.current.loading.sending).toBe(false);
    });

    it('should handle multiple loading flags simultaneously', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setLoading('chats', true);
        result.current.setLoading('games', true);
        result.current.setLoading('agents', true);
      });

      expect(result.current.loading.chats).toBe(true);
      expect(result.current.loading.games).toBe(true);
      expect(result.current.loading.agents).toBe(true);
      // Others should remain false
      expect(result.current.loading.messages).toBe(false);
      expect(result.current.loading.sending).toBe(false);
    });
  });
});

// ============================================================================
// Error State Tests
// ============================================================================

describe('uiSlice - Error State', () => {
  describe('setError action', () => {
    it('should set error message', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const errorMessage = 'Failed to load messages';
      act(() => {
        result.current.setError(errorMessage);
      });

      expect(result.current.error).toBe(errorMessage);
    });

    it('should overwrite previous error', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setError('First error');
      });
      expect(result.current.error).toBe('First error');

      act(() => {
        result.current.setError('Second error');
      });
      expect(result.current.error).toBe('Second error');
    });

    it('should accept null as error value', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setError('Some error');
        result.current.setError(null);
      });

      expect(result.current.error).toBeNull();
    });
  });

  describe('clearError action', () => {
    it('should clear existing error', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setError('Some error message');
      });
      expect(result.current.error).toBe('Some error message');

      act(() => {
        result.current.clearError();
      });
      expect(result.current.error).toBeNull();
    });

    it('should handle clearing when no error exists', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      expect(result.current.error).toBeNull();

      act(() => {
        result.current.clearError();
      });

      expect(result.current.error).toBeNull();
    });
  });
});

// ============================================================================
// Input Value Tests
// ============================================================================

describe('uiSlice - Input Value', () => {
  describe('setInputValue action', () => {
    it('should set input value', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const inputText = 'How do I setup the game?';
      act(() => {
        result.current.setInputValue(inputText);
      });

      expect(result.current.inputValue).toBe(inputText);
    });

    it('should update input value', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setInputValue('Initial text');
      });
      expect(result.current.inputValue).toBe('Initial text');

      act(() => {
        result.current.setInputValue('Updated text');
      });
      expect(result.current.inputValue).toBe('Updated text');
    });

    it('should clear input value with empty string', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setInputValue('Some text');
      });
      expect(result.current.inputValue).toBe('Some text');

      act(() => {
        result.current.setInputValue('');
      });
      expect(result.current.inputValue).toBe('');
    });

    it('should handle multiline input', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const multilineText = 'Line 1\nLine 2\nLine 3';
      act(() => {
        result.current.setInputValue(multilineText);
      });

      expect(result.current.inputValue).toBe(multilineText);
    });
  });
});

// ============================================================================
// Message Editing Tests
// ============================================================================

describe('uiSlice - Message Editing', () => {
  describe('startEdit action', () => {
    it('should set editing message id and content', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const messageId = 'msg-123';
      const content = 'Original message content';

      act(() => {
        result.current.startEdit(messageId, content);
      });

      expect(result.current.editingMessageId).toBe(messageId);
      expect(result.current.editContent).toBe(content);
    });

    it('should overwrite previous editing state', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.startEdit('msg-1', 'Content 1');
      });
      expect(result.current.editingMessageId).toBe('msg-1');
      expect(result.current.editContent).toBe('Content 1');

      act(() => {
        result.current.startEdit('msg-2', 'Content 2');
      });
      expect(result.current.editingMessageId).toBe('msg-2');
      expect(result.current.editContent).toBe('Content 2');
    });
  });

  describe('setEditContent action', () => {
    it('should update edit content', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.startEdit('msg-123', 'Original content');
      });

      const updatedContent = 'Updated content';
      act(() => {
        result.current.setEditContent(updatedContent);
      });

      expect(result.current.editContent).toBe(updatedContent);
      expect(result.current.editingMessageId).toBe('msg-123');
    });

    it('should handle empty edit content', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.startEdit('msg-123', 'Original content');
        result.current.setEditContent('');
      });

      expect(result.current.editContent).toBe('');
    });
  });

  describe('cancelEdit action', () => {
    it('should clear editing state', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.startEdit('msg-123', 'Some content');
      });
      expect(result.current.editingMessageId).toBe('msg-123');
      expect(result.current.editContent).toBe('Some content');

      act(() => {
        result.current.cancelEdit();
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });

    it('should handle canceling when not editing', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');

      act(() => {
        result.current.cancelEdit();
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });
  });

  describe('saveEdit action', () => {
    it('should call edit function and clear editing state on success', async () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const mockEditFn = jest.fn().mockResolvedValue(undefined);
      const messageId = 'msg-123';
      const editedContent = 'Edited content';

      act(() => {
        result.current.startEdit(messageId, editedContent);
      });

      await act(async () => {
        await result.current.saveEdit(mockEditFn);
      });

      expect(mockEditFn).toHaveBeenCalledWith(messageId, editedContent);
      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });

    it('should not call edit function if not editing', async () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const mockEditFn = jest.fn();

      await act(async () => {
        await result.current.saveEdit(mockEditFn);
      });

      expect(mockEditFn).not.toHaveBeenCalled();
    });

    it('should handle edit function error', async () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const mockError = new Error('Failed to save edit');
      const mockEditFn = jest.fn().mockRejectedValue(mockError);
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      act(() => {
        result.current.startEdit('msg-123', 'Content');
      });

      await expect(
        act(async () => {
          await result.current.saveEdit(mockEditFn);
        })
      ).rejects.toThrow('Failed to save edit');

      expect(consoleErrorSpy).toHaveBeenCalledWith('Failed to save edit:', mockError);
      consoleErrorSpy.mockRestore();
    });

    it('should preserve editing state on error', async () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      const mockEditFn = jest.fn().mockRejectedValue(new Error('Failed'));
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      act(() => {
        result.current.startEdit('msg-123', 'Content');
      });

      try {
        await act(async () => {
          await result.current.saveEdit(mockEditFn);
        });
      } catch {
        // Expected error
      }

      // Editing state should be preserved on error
      expect(result.current.editingMessageId).toBe('msg-123');
      expect(result.current.editContent).toBe('Content');

      consoleErrorSpy.mockRestore();
    });
  });
});

// ============================================================================
// Search Mode Tests
// ============================================================================

describe('uiSlice - Search Mode', () => {
  describe('setSearchMode action', () => {
    it('should set search mode to Vector', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setSearchMode('Vector');
      });

      expect(result.current.searchMode).toBe('Vector');
    });

    it('should set search mode to Keyword', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setSearchMode('Keyword');
      });

      expect(result.current.searchMode).toBe('Keyword');
    });

    it('should set search mode to Hybrid', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.setSearchMode('Keyword');
        result.current.setSearchMode('Hybrid');
      });

      expect(result.current.searchMode).toBe('Hybrid');
    });

    it('should toggle between search modes', () => {
      const useStore = createTestStore();
      const { result } = renderHook(() => useStore());

      expect(result.current.searchMode).toBe('Hybrid');

      act(() => {
        result.current.setSearchMode('Vector');
      });
      expect(result.current.searchMode).toBe('Vector');

      act(() => {
        result.current.setSearchMode('Keyword');
      });
      expect(result.current.searchMode).toBe('Keyword');

      act(() => {
        result.current.setSearchMode('Hybrid');
      });
      expect(result.current.searchMode).toBe('Hybrid');
    });
  });
});

// ============================================================================
// Integration Tests - Multiple Actions
// ============================================================================

describe('uiSlice - Integration', () => {
  it('should handle multiple simultaneous state updates', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    act(() => {
      result.current.setLoading('sending', true);
      result.current.setInputValue('Test message');
      result.current.setSearchMode('Vector');
      result.current.setError(null);
    });

    expect(result.current.loading.sending).toBe(true);
    expect(result.current.inputValue).toBe('Test message');
    expect(result.current.searchMode).toBe('Vector');
    expect(result.current.error).toBeNull();
  });

  it('should handle typical message sending workflow', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    // User types message
    act(() => {
      result.current.setInputValue('How do I play?');
    });
    expect(result.current.inputValue).toBe('How do I play?');

    // User clicks send
    act(() => {
      result.current.setLoading('sending', true);
      result.current.setInputValue(''); // Clear input
    });
    expect(result.current.loading.sending).toBe(true);
    expect(result.current.inputValue).toBe('');

    // Message sent successfully
    act(() => {
      result.current.setLoading('sending', false);
    });
    expect(result.current.loading.sending).toBe(false);
  });

  it('should handle message editing workflow', async () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    const mockEditFn = jest.fn().mockResolvedValue(undefined);

    // Start editing
    act(() => {
      result.current.startEdit('msg-123', 'Original content');
    });
    expect(result.current.editingMessageId).toBe('msg-123');
    expect(result.current.editContent).toBe('Original content');

    // User modifies content
    act(() => {
      result.current.setEditContent('Updated content');
    });
    expect(result.current.editContent).toBe('Updated content');

    // Save edit
    await act(async () => {
      await result.current.saveEdit(mockEditFn);
    });

    expect(mockEditFn).toHaveBeenCalledWith('msg-123', 'Updated content');
    expect(result.current.editingMessageId).toBeNull();
    expect(result.current.editContent).toBe('');
  });

  it('should handle error state during operations', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    // Start operation
    act(() => {
      result.current.setLoading('messages', true);
      result.current.clearError();
    });
    expect(result.current.loading.messages).toBe(true);
    expect(result.current.error).toBeNull();

    // Operation fails
    act(() => {
      result.current.setLoading('messages', false);
      result.current.setError('Failed to load messages');
    });
    expect(result.current.loading.messages).toBe(false);
    expect(result.current.error).toBe('Failed to load messages');

    // User dismisses error
    act(() => {
      result.current.clearError();
    });
    expect(result.current.error).toBeNull();
  });

  it('should maintain state independence', () => {
    const useStore = createTestStore();
    const { result } = renderHook(() => useStore());

    // Set various states
    act(() => {
      result.current.setLoading('chats', true);
      result.current.setInputValue('Test input');
      result.current.startEdit('msg-1', 'Edit content');
      result.current.setSearchMode('Vector');
      result.current.setError('Test error');
    });

    // Changing one state shouldn't affect others
    act(() => {
      result.current.setLoading('chats', false);
    });

    expect(result.current.loading.chats).toBe(false);
    expect(result.current.inputValue).toBe('Test input');
    expect(result.current.editingMessageId).toBe('msg-1');
    expect(result.current.editContent).toBe('Edit content');
    expect(result.current.searchMode).toBe('Vector');
    expect(result.current.error).toBe('Test error');
  });
});
