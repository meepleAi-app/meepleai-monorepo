/**
 * Game Detail Client Component (Issue #3152)
 *
 * Client-side component for game detail page with split view:
 * - Left: AI Agent chat interface
 * - Right: PDF viewer
 *
 * Features:
 * - Resizable panels
 * - Agent mode selection
 * - PDF selection
 * - Chat-to-PDF navigation
 * - Mobile toggle view
 */

'use client';

import { useState, useCallback } from 'react';

import { ArrowLeft, MoreVertical } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { AgentChatPanel, type AgentMode, type GamePdf } from '@/components/game-detail';
import { SplitViewLayout } from '@/components/game-detail/SplitViewLayout';
import { PdfViewer } from '@/components/pdf-viewer';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import { AgentConfigModal } from '@/components/agent/AgentConfigModal'; // Issue #3186

export interface GameDetailClientProps {
  gameId: string;
  gameTitle: string;
  gamePublisher?: string | null;
  gameImageUrl?: string | null;
  /** Available agent modes for this game */
  agentModes: AgentMode[];
  /** Available PDFs for this game */
  availablePdfs: GamePdf[];
}

export default function GameDetailClient({
  gameId,
  gameTitle,
  gamePublisher,
  gameImageUrl,
  agentModes,
  availablePdfs,
}: GameDetailClientProps) {
  const [currentPdfUrl, setCurrentPdfUrl] = useState<string>(
    availablePdfs[0]?.id || '' // TODO: Map to actual URL
  );
  const [highlightedPage, setHighlightedPage] = useState<number | undefined>();
  const [mobileView, setMobileView] = useState<'chat' | 'pdf'>('chat');

  // Handle PDF reference click from chat
  const handlePdfReferenceClick = useCallback((pageNumber: number, pdfId: string) => {
    // Switch to PDF if needed (for PDF in list)
    if (pdfId !== currentPdfUrl) {
      setCurrentPdfUrl(pdfId);
    }
    // Trigger page jump
    setHighlightedPage(pageNumber);
    // Reset highlight after animation
    setTimeout(() => setHighlightedPage(undefined), 1000);

    // On mobile, switch to PDF view
    setMobileView('pdf');
  }, [currentPdfUrl]);

  return (
    <div className="flex flex-col h-screen">
      {/* Header Bar */}
      <header className="bg-white border-b border-gray-200 px-4 md:px-6 py-4 flex items-center justify-between shadow-sm flex-shrink-0">
        <div className="flex items-center gap-3 md:gap-4 flex-1 min-w-0">
          <Button variant="ghost" size="sm" asChild>
            <Link href="/library">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>

          {gameImageUrl && (
            <Image
              src={gameImageUrl}
              alt={gameTitle}
              width={48}
              height={48}
              className="rounded-lg object-cover shadow-sm hidden sm:block"
            />
          )}

          <div className="flex-1 min-w-0">
            <h1 className="font-quicksand text-lg md:text-xl font-bold truncate">
              {gameTitle}
            </h1>
            {gamePublisher && (
              <p className="text-sm text-muted-foreground truncate">{gamePublisher}</p>
            )}
          </div>
        </div>

        {/* Actions Menu */}
        <div className="flex items-center gap-2">
          {/* Agent Config Modal - Issue #3186 */}
          <AgentConfigModal
            gameId={gameId}
            trigger={
              <Button variant="outline" size="sm">
                AI Config
              </Button>
            }
            onConfigSaved={() => {
              // Note: Chat sidebar integration pending (Issue #3186)
              console.log('Agent config saved');
            }}
          />

          <Button variant="ghost" size="sm">
            <MoreVertical className="h-4 w-4" />
          </Button>
        </div>
      </header>

      {/* Mobile View Toggle */}
      <div className="lg:hidden flex border-b border-gray-200 bg-white">
        <button
          onClick={() => setMobileView('chat')}
          className={cn(
            'flex-1 px-4 py-3 font-medium text-sm transition-colors',
            mobileView === 'chat'
              ? 'border-b-2 border-blue-600 text-blue-600'
              : 'text-gray-600 hover:text-gray-900'
          )}
        >
          <div className="flex items-center justify-center gap-2">
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"
              />
            </svg>
            Chat
          </div>
        </button>
        <button
          onClick={() => setMobileView('pdf')}
          className={cn(
            'flex-1 px-4 py-3 font-medium text-sm transition-colors',
            mobileView === 'pdf'
              ? 'border-b-2 border-blue-600 text-blue-600'
              : 'text-gray-600 hover:text-gray-900'
          )}
        >
          <div className="flex items-center justify-center gap-2">
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
              />
            </svg>
            PDF
          </div>
        </button>
      </div>

      {/* Desktop: Split View */}
      <div className="hidden lg:flex flex-1 overflow-hidden">
        <SplitViewLayout
          leftPanel={
            <AgentChatPanel
              gameId={gameId}
              gameTitle={gameTitle}
              agentModes={agentModes}
              availablePdfs={availablePdfs}
              onPdfReferenceClick={handlePdfReferenceClick}
            />
          }
          rightPanel={
            <PdfViewer
              pdfUrl={currentPdfUrl}
              highlightedPage={highlightedPage}
              showControls={true}
            />
          }
        />
      </div>

      {/* Mobile: Toggle View */}
      <div className="lg:hidden flex-1 overflow-hidden">
        {mobileView === 'chat' ? (
          <AgentChatPanel
            gameId={gameId}
            gameTitle={gameTitle}
            agentModes={agentModes}
            availablePdfs={availablePdfs}
            onPdfReferenceClick={handlePdfReferenceClick}
          />
        ) : (
          <PdfViewer
            pdfUrl={currentPdfUrl}
            highlightedPage={highlightedPage}
            showControls={true}
          />
        )}
      </div>
    </div>
  );
}
