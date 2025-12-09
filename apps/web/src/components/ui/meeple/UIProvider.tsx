/**
 * UIProvider - UI state management
 *
 * Manages:
 * - Sidebar collapsed state
 * - Message editing state
 * - Input value state
 * - Search mode state
 *
 * Nested under ChatProvider in provider hierarchy
 */

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  useMemo,
  PropsWithChildren,
} from 'react';

// ============================================================================
// Types
// ============================================================================

export interface UIContextValue {
  // Sidebar
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;

  // Message editing
  editingMessageId: string | null;
  editContent: string;
  startEdit: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: (editMessageFn: (messageId: string, content: string) => Promise<void>) => Promise<void>;
  setEditContent: (content: string) => void;

  // Input
  inputValue: string;
  setInputValue: (value: string) => void;

  // Search mode
  searchMode: string;
  setSearchMode: (mode: string) => void;
}

// ============================================================================
// Context
// ============================================================================

const UIContext = createContext<UIContextValue | null>(null);

// ============================================================================
// Provider Component
// ============================================================================

export function UIProvider({ children }: PropsWithChildren) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState('');
  const [inputValue, setInputValue] = useState('');
  const [searchMode, setSearchMode] = useState('Hybrid');

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed(prev => !prev);
  }, []);

  const startEdit = useCallback((messageId: string, content: string) => {
    setEditingMessageId(messageId);
    setEditContent(content);
  }, []);

  const cancelEdit = useCallback(() => {
    setEditingMessageId(null);
    setEditContent('');
  }, []);

  const saveEdit = useCallback(
    async (editMessageFn: (messageId: string, content: string) => Promise<void>) => {
      if (!editingMessageId) return;

      try {
        await editMessageFn(editingMessageId, editContent);
        setEditingMessageId(null);
        setEditContent('');
      } catch (err) {
        console.error('Failed to save edit:', err);
        throw err;
      }
    },
    [editingMessageId, editContent]
  );

  const value = useMemo<UIContextValue>(
    () => ({
      sidebarCollapsed,
      toggleSidebar,
      editingMessageId,
      editContent,
      startEdit,
      cancelEdit,
      saveEdit,
      setEditContent,
      inputValue,
      setInputValue,
      searchMode,
      setSearchMode,
    }),
    [
      sidebarCollapsed,
      toggleSidebar,
      editingMessageId,
      editContent,
      startEdit,
      cancelEdit,
      saveEdit,
      inputValue,
      searchMode,
    ]
  );

  return <UIContext.Provider value={value}>{children}</UIContext.Provider>;
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to access UI context
 * Throws error if used outside UIProvider
 */
export function useUI(): UIContextValue {
  const context = useContext(UIContext);
  if (!context) {
    throw new Error('useUI must be used within UIProvider');
  }
  return context;
}
