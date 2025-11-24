/**
 * Backward compatibility layer for useChatContext
 *
 * This file provides a unified interface from Zustand store:
 * - useAuth() - Authentication state
 * - useChatContext (Zustand) - Game selection, chat ops, UI state, messages
 *
 * This maintains compatibility with components that were using
 * the old monolithic ChatProvider context.
 *
 * Issue #1083: Zustand Chat Store Migration
 */

import React from 'react';
import { useAuth } from '@/components/auth/AuthProvider';
import { useChatContextCompat, ChatContextValue as ChatContextValueCompat } from '@/store/chat/ChatStoreProvider';

// Export ChatContextValue from compatibility layer
export type ChatContextValue = ChatContextValueCompat;

/**
 * Unified hook that combines all chat-related contexts
 * Maintains backward compatibility with old ChatProvider API
 * Issue #1007: Now includes SSE streaming state
 */
export function useChatContext(): ChatContextValue {
  const zustandContext = useChatContextCompat();

  // Return context directly - it already includes all properties including streaming (Issue #1007)
  return zustandContext;
}
