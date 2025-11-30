'use client';

/**
 * Chat Page Client Component
 *
 * Main chat interface with Zustand store for state management.
 * Extracted from Pages Router for App Router compatibility.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router
 * Issue #1080: FE-IMP-004 - AuthContext + Edge Middleware
 * Issue #1083: Zustand Chat Store Migration
 *
 * Note: Authentication is now handled by middleware.ts, which redirects
 * unauthenticated users to /login before this component renders.
 * This eliminates flash of unauthorized content.
 */

import { useState } from 'react';
import { ChatStoreProvider } from '@/store/chat/ChatStoreProvider';
import { ChatSidebar } from '@/components/chat/ChatSidebar';
import { ChatContent } from '@/components/chat/ChatContent';
import { BottomNav } from '@/components/layout/BottomNav';
import { ExportChatModal } from '@/components/modals';

export default function ChatPage() {
  const [showExportModal, setShowExportModal] = useState(false);

  // Main chat interface with Zustand store (Issue #1083)
  // Auth check is now handled by middleware.ts (Issue #1080)
  return (
    <ChatStoreProvider>
      <main id="main-content" className="flex h-dvh font-sans overflow-hidden">
        <ChatSidebar />
        <ChatContent />
        <BottomNav />

        <ExportChatModal
          isOpen={showExportModal}
          onClose={() => setShowExportModal(false)}
          chatId={'temp-placeholder'}
          gameName={'placeholder'}
        />
      </main>
    </ChatStoreProvider>
  );
}
