/**
 * UI Slice (Issue #1083)
 *
 * Manages UI state:
 * - Loading states for all operations
 * - Error messages
 * - Message input field value
 * - Message editing state
 * - Search mode toggle
 *
 * Consolidates UIProvider functionality into Zustand.
 */

import { StateCreator } from 'zustand';
import { ChatStore, UISlice } from '../types';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

export const createUISlice: StateCreator<
  ChatStore,
  [['zustand/immer', never], ['zustand/subscribeWithSelector', never]],
  [],
  UISlice
> = (set, get) => ({
  // ============================================================================
  // State
  // ============================================================================
  loading: {
    chats: false,
    messages: false,
    sending: false,
    creating: false,
    updating: false,
    deleting: false,
    games: false,
    agents: false,
  },
  error: null,
  inputValue: '',
  editingMessageId: null,
  editContent: '',
  searchMode: 'Hybrid',

  // ============================================================================
  // Actions - Loading & Errors
  // ============================================================================
  setLoading: (key, value) =>
    set((state) => {
      state.loading[key] = value;
    }),

  setError: (error) =>
    set((state) => {
      state.error = error;
    }),

  clearError: () =>
    set((state) => {
      state.error = null;
    }),

  // ============================================================================
  // Actions - Input
  // ============================================================================
  setInputValue: (value) =>
    set((state) => {
      state.inputValue = value;
    }),

  // ============================================================================
  // Actions - Message Editing
  // ============================================================================
  startEdit: (messageId, content) =>
    set((state) => {
      state.editingMessageId = messageId;
      state.editContent = content;
    }),

  cancelEdit: () =>
    set((state) => {
      state.editingMessageId = null;
      state.editContent = '';
    }),

  saveEdit: async (editMessageFn) => {
    const { editingMessageId, editContent } = get();

    if (!editingMessageId) return;

    try {
      await editMessageFn(editingMessageId, editContent);
      set((state) => {
        state.editingMessageId = null;
        state.editContent = '';
      });
    } catch (err) {
      logger.error(
        'Failed to save edit',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('UiSlice', 'saveEdit', { messageId: editingMessageId })
      );
      throw err;
    }
  },

  setEditContent: (content) =>
    set((state) => {
      state.editContent = content;
    }),

  // ============================================================================
  // Actions - Search Mode
  // ============================================================================
  setSearchMode: (mode) =>
    set((state) => {
      state.searchMode = mode;
    }),
});
