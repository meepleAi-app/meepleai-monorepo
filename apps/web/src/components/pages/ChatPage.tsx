'use client';

/**
 * Chat Page Client Component - Issue #2232
 *
 * Main chat interface with ChatLayout system.
 * Refactored to use ChatLayout pattern (Issue #2232).
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router
 * Issue #1080: FE-IMP-004 - AuthContext + Edge Middleware
 * Issue #1083: Zustand Chat Store Migration
 * Issue #2232: ChatLayout System Implementation
 *
 * Note: Authentication is now handled by middleware.ts, which redirects
 * unauthenticated users to /login before this component renders.
 * This eliminates flash of unauthorized content.
 */

import { useState } from 'react';

import { ChatContent } from '@/components/chat/ChatContent';
import { ChatSidebar } from '@/components/chat/ChatSidebar';
import { ShareChatModal } from '@/components/chat/ShareChatModal';
import { BottomNav } from '@/components/layout/BottomNav';
import { ChatLayout } from '@/components/layouts/ChatLayout';
import { ExportChatModal } from '@/components/modals';
import { useThreadDeletion } from '@/hooks/useThreadDeletion';
import { ChatStoreProvider } from '@/store/chat/ChatStoreProvider';
import { useChatStore } from '@/store/chat/store';
import { Game, ChatThread } from '@/types';

function ChatPageContent() {
  const [showExportModal, setShowExportModal] = useState(false);
  const [showShareModal, setShowShareModal] = useState(false);

  // Thread deletion hook (Issue #2258)
  const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

  // Connect to Zustand store for header props
  const {
    games,
    selectedGameId,
    selectGame,
    chatsByGame,
    activeChatIds,
    loading,
    updateChatTitle,
  } = useChatStore(state => ({
    games: state.games,
    selectedGameId: state.selectedGameId,
    selectGame: state.selectGame,
    chatsByGame: state.chatsByGame,
    activeChatIds: state.activeChatIds,
    loading: state.loading,
    updateChatTitle: state.updateChatTitle,
  }));

  // Derived values
  // eslint-disable-next-line security/detect-object-injection -- Safe: selectedGameId is user-selected, not external input
  const activeChatId = selectedGameId ? activeChatIds[selectedGameId] : null;
  // eslint-disable-next-line security/detect-object-injection -- Safe: selectedGameId is user-selected, not external input
  const chats = selectedGameId ? (chatsByGame[selectedGameId] ?? []) : [];
  const activeThread = chats.find((c: ChatThread) => c.id === activeChatId);
  const selectedGame = games.find((g: Game) => g.id === selectedGameId);

  const handleGameChange = (gameId: string) => {
    void selectGame(gameId);
  };

  const handleTitleChange = (title: string) => {
    if (activeChatId) {
      void updateChatTitle(activeChatId, title);
    }
  };

  const handleShare = () => {
    setShowShareModal(true);
  };

  const handleExport = () => {
    setShowExportModal(true);
  };

  const handleDelete = () => {
    if (activeChatId) {
      void handleThreadDelete(activeChatId);
    }
  };

  return (
    <>
      <ChatLayout
        sidebarContent={<ChatSidebar />}
        game={selectedGame}
        games={games}
        onGameChange={handleGameChange}
        threadTitle={activeThread?.title ?? 'Untitled Thread'}
        onTitleChange={handleTitleChange}
        onShare={activeChatId ? handleShare : undefined}
        onExport={activeChatId ? handleExport : undefined}
        onDelete={activeChatId ? handleDelete : undefined}
        loading={{
          games: loading.games,
          title: false,
        }}
      >
        <ChatContent />
      </ChatLayout>

      <BottomNav />

      <ExportChatModal
        isOpen={showExportModal}
        onClose={() => setShowExportModal(false)}
        chatId={activeChatId ?? ''}
        gameName={selectedGame?.title ?? ''}
      />

      {activeThread && activeChatId && (
        <ShareChatModal
          isOpen={showShareModal}
          onClose={() => setShowShareModal(false)}
          threadId={activeChatId}
        />
      )}

      {/* Confirmation dialog for thread deletion (Issue #2258) */}
      <ConfirmDialogComponent />
    </>
  );
}

export default function ChatPage() {
  // Main chat interface with Zustand store (Issue #1083)
  // Auth check is now handled by middleware.ts (Issue #1080)
  return (
    <ChatStoreProvider>
      <main id="main-content" className="flex h-dvh font-sans overflow-hidden">
        <ChatPageContent />
      </main>
    </ChatStoreProvider>
  );
}
