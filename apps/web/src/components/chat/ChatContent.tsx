/* eslint-disable security/detect-object-injection -- Safe Zustand store key access */
/**
 * ChatContent - Main chat content area (Issue #858, BGAI-074)
 *
 * Composes the message list and input form.
 * Displays thread header with title, game info, message count, and status.
 *
 * Updated for SPRINT-3 #858:
 * - Thread-based header (title, message count, status)
 * - Shows archived thread indicator
 * - Better mobile spacing (prevents bottom nav overlap)
 *
 * Updated for BGAI-074:
 * - Citation click → PDF page jump functionality
 * - PdfViewerModal integration for viewing PDF at specific page
 *
 * Will be enhanced in Phase 4 with:
 * - Export thread button
 * - Streaming response display with stop button
 * - Typing indicator
 * - Full integration with useChatStreaming hook
 */

import React, { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useChatStore } from '@/store/chat/store';
import { MessageList } from './MessageList';
import { MessageInput } from './MessageInput';
import { MobileSidebar } from './MobileSidebar';
import { ContextChip } from './ContextChip';
import { PdfViewerModal } from '@/components/pdf/PdfViewerModal';
import { api } from '@/lib/api';
import { Game, ChatThread } from '@/types';

/**
 * PDF modal state for citation click → jump to page (BGAI-074)
 */
interface PdfModalState {
  open: boolean;
  pdfUrl: string;
  pageNumber: number;
  documentName: string;
}

const INITIAL_PDF_MODAL_STATE: PdfModalState = {
  open: false,
  pdfUrl: '',
  pageNumber: 1,
  documentName: '',
};

export function ChatContent() {
  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const {
    games,
    selectedGameId,
    chatsByGame,
    activeChatIds,
    messagesByChat,
    errorMessage,
    sidebarCollapsed,
    toggleSidebar,
  } = useChatStore(state => ({
    games: state.games,
    selectedGameId: state.selectedGameId,
    chatsByGame: state.chatsByGame,
    activeChatIds: state.activeChatIds,
    messagesByChat: state.messagesByChat,
    errorMessage: state.error ?? '',
    sidebarCollapsed: state.sidebarCollapsed,
    toggleSidebar: state.toggleSidebar,
  }));

  // Derived values
  const activeChatId = selectedGameId ? activeChatIds[selectedGameId] : null;
  const chats = selectedGameId ? (chatsByGame[selectedGameId] ?? []) : [];
  const _messages = activeChatId ? (messagesByChat[activeChatId] ?? []) : [];

  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [isMobile, setIsMobile] = useState(false);

  // PDF modal state for citation click → jump to page (BGAI-074)
  const [pdfModal, setPdfModal] = useState<PdfModalState>(INITIAL_PDF_MODAL_STATE);

  const selectedGame = games.find((g: Game) => g.id === selectedGameId);
  const activeThread = chats.find((c: ChatThread) => c.id === activeChatId);
  const isArchived = activeThread?.status === 'Closed';

  /**
   * Handle citation click → open PDF at specific page (BGAI-074, Issue #1940)
   *
   * Receives documentId and pageNumber directly from clicked citation,
   * then opens PdfViewerModal at that page.
   *
   * Fix for Issue #1940: No longer searches through messages - receives
   * exact page number from citation click, ensuring correct page is opened.
   */
  const handleCitationClick = useCallback(
    (documentId: string, pageNumber: number) => {
      const pdfUrl = api.pdf.getPdfDownloadUrl(documentId);
      setPdfModal({
        open: true,
        pdfUrl,
        pageNumber,
        documentName: selectedGame?.title || 'PDF Document',
      });
    },
    [selectedGame?.title]
  );

  /**
   * Close PDF modal (BGAI-074)
   */
  const handlePdfModalClose = useCallback((open: boolean) => {
    if (!open) {
      setPdfModal(INITIAL_PDF_MODAL_STATE);
    }
  }, []);

  // Track mobile viewport with matchMedia
  useEffect(() => {
    const mediaQuery = window.matchMedia('(max-width: 767px)');
    setIsMobile(mediaQuery.matches);

    const handleChange = (e: MediaQueryListEvent) => {
      setIsMobile(e.matches);
    };

    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, []);

  const handleToggleSidebar = () => {
    // On mobile (< 768px), toggle Sheet instead of desktop sidebar
    if (isMobile) {
      setMobileMenuOpen(!mobileMenuOpen);
    } else {
      toggleSidebar();
    }
  };

  return (
    <div className="flex-1 flex flex-col bg-white">
      {/* Mobile Sidebar (from origin/main) */}
      <MobileSidebar open={mobileMenuOpen} onOpenChange={setMobileMenuOpen} />

      {/* Header (Issue #858: Thread-based) */}
      <div className="p-4 border-b border-[#dadce0] flex justify-between items-center bg-white">
        <div className="flex items-center gap-3 flex-1 min-w-0">
          <button
            onClick={handleToggleSidebar}
            aria-label={sidebarCollapsed ? 'Show sidebar' : 'Hide sidebar'}
            aria-expanded={!sidebarCollapsed}
            className="px-3 py-2 bg-[#f1f3f4] border-none rounded cursor-pointer text-lg touch-target hover:bg-[#e8eaed] transition-colors flex-shrink-0"
            title={sidebarCollapsed ? 'Mostra sidebar' : 'Nascondi sidebar'}
          >
            {sidebarCollapsed ? '☰' : '✕'}
          </button>
          <div className="flex-1 min-w-0">
            {/* Thread Title (Issue #858) */}
            <div className="flex items-center gap-2">
              <h1 className="m-0 text-xl truncate">
                {activeChatId
                  ? (activeThread?.title ?? 'Chat Thread')
                  : 'Seleziona o crea un thread'}
              </h1>
              {/* Archived Badge (Issue #858) */}
              {isArchived && (
                <span
                  className="px-2 py-0.5 bg-[#dadce0] text-[#5f6368] rounded text-[10px] font-semibold uppercase flex-shrink-0"
                  title="This thread is archived"
                  aria-label="Archived thread"
                >
                  Archiviato
                </span>
              )}
            </div>
            {/* Thread Metadata (Issue #858) */}
            <div className="mt-1 mb-0 text-[#64748b] text-[13px] flex items-center gap-2">
              <span>
                {selectedGameId ? (selectedGame?.title ?? '') : 'Nessun gioco selezionato'}
              </span>
              {activeThread && (
                <>
                  <span>•</span>
                  <span>{activeThread.messageCount} messaggi</span>
                </>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          <Link
            href="/"
            className="px-4 py-2 bg-[#1a73e8] text-white no-underline rounded text-sm hover:bg-[#1557b0] transition-colors"
          >
            Home
          </Link>
        </div>
      </div>

      {/* Error Message */}
      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="m-4 p-3 bg-[#fce8e6] text-[#d93025] rounded text-sm"
        >
          {errorMessage}
        </div>
      )}

      {/* Context Chip (Issue #1840) */}
      {selectedGameId && selectedGame && (
        <div className="px-4 pt-4">
          <ContextChip
            gameName={selectedGame.title}
            gameEmoji="🎲"
            sources={[{ type: 'PDF', count: 1 }, { type: 'FAQ', count: 15 }, { type: 'Wiki' }]}
          />
        </div>
      )}

      {/* Messages Area */}
      <MessageList onCitationClick={handleCitationClick} />

      {/* Message Input */}
      <MessageInput />

      {/* PDF Viewer Modal (BGAI-074) */}
      <PdfViewerModal
        open={pdfModal.open}
        onOpenChange={handlePdfModalClose}
        pdfUrl={pdfModal.pdfUrl}
        initialPage={pdfModal.pageNumber}
        documentName={pdfModal.documentName}
      />
    </div>
  );
}
