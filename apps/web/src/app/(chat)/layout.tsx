/**
 * Chat Route Group Layout
 * Issue #2233 - Phase 4: Route Groups
 *
 * Applies ChatLayout to all pages in (chat) group:
 * - /chat
 * - /shared/chat
 *
 * Features:
 * - ChatHeader with game selector
 * - Collapsible thread sidebar
 * - Mobile responsive with Sheet
 * - Full height layout for messages
 *
 * Note: ChatLayout requires specific props (threads, game context)
 * which are passed from page components, not this layout wrapper.
 */

'use client';

import { ReactNode } from 'react';

/**
 * Chat layout wrapper
 *
 * Unlike PublicLayout, ChatLayout has complex state management
 * (threads, games, sidebar) that varies per page.
 *
 * This wrapper just provides a consistent container.
 * Individual chat pages use <ChatLayout /> directly with their props.
 */
export default function ChatRootLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
