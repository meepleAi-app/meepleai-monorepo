/**
 * Tests for uiSlice (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: UI state management, loading states, message editing, search mode
 */

import { useChatStore } from '../../store';
import { resetChatStore } from './chatSlice.test-helpers';

describe('uiSlice', () => {
  beforeEach(() => {
    resetChatStore(useChatStore);
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should initialize with default UI state', () => {
      const state = useChatStore.getState();

      expect(state.loading).toEqual({
        chats: false,
        messages: false,
        sending: false,
        creating: false,
        updating: false,
        deleting: false,
        games: false,
        agents: false,
      });
      expect(state.error).toBeNull();
      expect(state.inputValue).toBe('');
      expect(state.editingMessageId).toBeNull();
      expect(state.editContent).toBe('');
      expect(state.searchMode).toBe('Hybrid');
    });
  });

  describe('Loading State Management', () => {
    it('should set loading state for specific key', () => {
      useChatStore.getState().setLoading('games', true);

      const state = useChatStore.getState();
      expect(state.loading.games).toBe(true);
      expect(state.loading.chats).toBe(false); // Others unchanged
    });

    it('should handle multiple loading keys', () => {
      useChatStore.getState().setLoading('chats', true);
      useChatStore.getState().setLoading('messages', true);
      useChatStore.getState().setLoading('sending', true);

      const state = useChatStore.getState();
      expect(state.loading.chats).toBe(true);
      expect(state.loading.messages).toBe(true);
      expect(state.loading.sending).toBe(true);
      expect(state.loading.games).toBe(false);
    });

    it('should toggle loading state', () => {
      useChatStore.getState().setLoading('creating', true);
      expect(useChatStore.getState().loading.creating).toBe(true);

      useChatStore.getState().setLoading('creating', false);
      expect(useChatStore.getState().loading.creating).toBe(false);
    });
  });

  describe('Error State Management', () => {
    it('should set error message', () => {
      useChatStore.getState().setError('Something went wrong');

      expect(useChatStore.getState().error).toBe('Something went wrong');
    });

    it('should clear error', () => {
      useChatStore.getState().setError('Error message');
      expect(useChatStore.getState().error).toBe('Error message');

      useChatStore.getState().clearError();
      expect(useChatStore.getState().error).toBeNull();
    });

    it('should overwrite previous error', () => {
      useChatStore.getState().setError('First error');
      useChatStore.getState().setError('Second error');

      expect(useChatStore.getState().error).toBe('Second error');
    });
  });

  describe('Input Value Management', () => {
    it('should set input value', () => {
      useChatStore.getState().setInputValue('User message');

      expect(useChatStore.getState().inputValue).toBe('User message');
    });

    it('should update input value', () => {
      useChatStore.getState().setInputValue('First');
      useChatStore.getState().setInputValue('Second');

      expect(useChatStore.getState().inputValue).toBe('Second');
    });

    it('should clear input value', () => {
      useChatStore.getState().setInputValue('Some text');
      useChatStore.getState().setInputValue('');

      expect(useChatStore.getState().inputValue).toBe('');
    });
  });

  describe('Message Editing', () => {
    it('should start edit mode', () => {
      useChatStore.getState().startEdit('msg-123', 'Original message');

      const state = useChatStore.getState();
      expect(state.editingMessageId).toBe('msg-123');
      expect(state.editContent).toBe('Original message');
    });

    it('should cancel edit mode', () => {
      useChatStore.getState().startEdit('msg-123', 'Message');
      useChatStore.getState().cancelEdit();

      const state = useChatStore.getState();
      expect(state.editingMessageId).toBeNull();
      expect(state.editContent).toBe('');
    });

    it('should update edit content', () => {
      useChatStore.getState().startEdit('msg-123', 'Original');
      useChatStore.getState().setEditContent('Updated content');

      expect(useChatStore.getState().editContent).toBe('Updated content');
      expect(useChatStore.getState().editingMessageId).toBe('msg-123');
    });

    it('should save edit successfully', async () => {
      useChatStore.getState().startEdit('msg-123', 'Original');
      useChatStore.getState().setEditContent('Edited message');

      const editFn = vi.fn().mockResolvedValueOnce(undefined);

      await useChatStore.getState().saveEdit(editFn);

      expect(editFn).toHaveBeenCalledWith('msg-123', 'Edited message');

      const state = useChatStore.getState();
      expect(state.editingMessageId).toBeNull();
      expect(state.editContent).toBe('');
    });

    it('should handle save edit errors', async () => {
      useChatStore.getState().startEdit('msg-123', 'Content');

      const editFn = vi.fn().mockRejectedValueOnce(new Error('Save failed'));

      await expect(useChatStore.getState().saveEdit(editFn)).rejects.toThrow('Save failed');

      // Editing state should remain (not cleared on error)
      const state = useChatStore.getState();
      expect(state.editingMessageId).toBe('msg-123');
      expect(state.editContent).toBe('Content');
    });

    it('should do nothing if no message being edited', async () => {
      // Ensure clean state (previous test may have set editing state)
      useChatStore.getState().cancelEdit();

      const editFn = vi.fn();

      await useChatStore.getState().saveEdit(editFn);

      expect(editFn).not.toHaveBeenCalled();
    });
  });

  describe('Search Mode', () => {
    it('should set search mode to Semantic', () => {
      useChatStore.getState().setSearchMode('Semantic');

      expect(useChatStore.getState().searchMode).toBe('Semantic');
    });

    it('should set search mode to Keyword', () => {
      useChatStore.getState().setSearchMode('Keyword');

      expect(useChatStore.getState().searchMode).toBe('Keyword');
    });

    it('should set search mode to Hybrid', () => {
      useChatStore.getState().setSearchMode('Hybrid');

      expect(useChatStore.getState().searchMode).toBe('Hybrid');
    });

    it('should toggle between search modes', () => {
      expect(useChatStore.getState().searchMode).toBe('Hybrid');

      useChatStore.getState().setSearchMode('Semantic');
      expect(useChatStore.getState().searchMode).toBe('Semantic');

      useChatStore.getState().setSearchMode('Keyword');
      expect(useChatStore.getState().searchMode).toBe('Keyword');
    });
  });
});
