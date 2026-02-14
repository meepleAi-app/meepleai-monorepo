/**
 * ChatInfoPanel - Right-side contextual panel for chat (Issue #4365)
 *
 * Shows game info, agent info, RAG citations, and suggested questions
 * alongside the active chat conversation.
 *
 * Responsive behavior:
 * - Desktop (lg+): Fixed side panel, collapsible with localStorage persistence
 * - Tablet (md): Sheet drawer from right side
 * - Mobile (<md): Bottom sheet drawer
 *
 * Sections:
 * 1. Game & Agent compact MeepleCards
 * 2. Real-time RAG citations list
 * 3. Suggested follow-up questions
 */

import React, { useEffect, useState } from 'react';

import { BookOpen, Bot, ChevronRight, Gamepad2, Info, Lightbulb, PanelRightClose, PanelRightOpen, FileText } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { CitationLink } from '@/components/ui/data-display/citation-link';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { cn } from '@/lib/utils';
import { useChatInfoStore } from '@/store/chat-info/store';
import type { Citation } from '@/types';

// ============================================================================
// Types
// ============================================================================

export interface ChatInfoPanelGame {
  id: string;
  title: string;
  imageUrl?: string;
  publisher?: string;
  playerCount?: string;
  playTime?: string;
}

export interface ChatInfoPanelAgent {
  id: string;
  name: string;
  type: string;
  description?: string;
  avatarUrl?: string;
}

export interface ChatInfoPanelProps {
  /** Game context for the chat */
  game?: ChatInfoPanelGame | null;
  /** Agent handling the chat */
  agent?: ChatInfoPanelAgent | null;
  /** RAG citations from assistant messages */
  citations?: Citation[];
  /** Suggested follow-up questions */
  suggestedQuestions?: string[];
  /** Handler for citation click (jump to PDF page) */
  onCitationClick?: (documentId: string, pageNumber: number) => void;
  /** Handler for suggested question click */
  onQuestionClick?: (question: string) => void;
  /** Whether chat is currently streaming */
  isStreaming?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Desktop Panel Width
// ============================================================================

const PANEL_WIDTH = 'w-80';

// ============================================================================
// Sub-components
// ============================================================================

function SectionHeader({ icon: Icon, title }: { icon: React.ElementType; title: string }) {
  return (
    <div className="flex items-center gap-2 mb-3">
      <Icon className="h-4 w-4 text-muted-foreground" />
      <h3 className="text-sm font-semibold font-quicksand text-foreground">{title}</h3>
    </div>
  );
}

function GameSection({ game }: { game: ChatInfoPanelGame }) {
  return (
    <div data-testid="chat-info-game-section">
      <SectionHeader icon={Gamepad2} title="Gioco" />
      <MeepleCard
        entity="game"
        variant="compact"
        title={game.title}
        subtitle={game.publisher}
        imageUrl={game.imageUrl}
        metadata={[
          ...(game.playerCount ? [{ value: game.playerCount }] : []),
          ...(game.playTime ? [{ value: game.playTime }] : []),
        ]}
        data-testid="chat-info-game-card"
      />
    </div>
  );
}

function AgentSection({ agent }: { agent: ChatInfoPanelAgent }) {
  return (
    <div data-testid="chat-info-agent-section">
      <SectionHeader icon={Bot} title="Agente AI" />
      <MeepleCard
        entity="agent"
        variant="compact"
        title={agent.name}
        subtitle={agent.description || agent.type}
        avatarUrl={agent.avatarUrl}
        data-testid="chat-info-agent-card"
      />
    </div>
  );
}

function CitationsSection({
  citations,
  onCitationClick,
}: {
  citations: Citation[];
  onCitationClick?: (documentId: string, pageNumber: number) => void;
}) {
  if (citations.length === 0) {
    return (
      <div data-testid="chat-info-citations-section">
        <SectionHeader icon={BookOpen} title="Fonti" />
        <p className="text-xs text-muted-foreground italic">
          Le citazioni appariranno qui durante la conversazione.
        </p>
      </div>
    );
  }

  // Deduplicate citations by documentId+pageNumber
  const uniqueCitations = citations.reduce<Citation[]>((acc, citation) => {
    const exists = acc.some(
      c => c.documentId === citation.documentId && c.pageNumber === citation.pageNumber
    );
    if (!exists) acc.push(citation);
    return acc;
  }, []);

  return (
    <div data-testid="chat-info-citations-section">
      <SectionHeader icon={BookOpen} title={`Fonti (${uniqueCitations.length})`} />
      <div className="space-y-2 max-h-48 overflow-y-auto">
        {uniqueCitations.map((citation, index) => (
          <div
            key={`${citation.documentId}-${citation.pageNumber}-${index}`}
            className="flex items-start gap-2 p-2 rounded-lg bg-card/50 hover:bg-card/80 transition-colors"
          >
            <FileText className="h-3.5 w-3.5 text-orange-500 mt-0.5 flex-shrink-0" />
            <div className="flex-1 min-w-0">
              <p className="text-xs text-foreground line-clamp-2 mb-1">
                {citation.snippet}
              </p>
              <CitationLink
                pageNumber={citation.pageNumber}
                onClick={
                  onCitationClick
                    ? () => onCitationClick(citation.documentId, citation.pageNumber)
                    : undefined
                }
                className="text-[10px]"
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function SuggestedQuestionsSection({
  questions,
  onQuestionClick,
  disabled,
}: {
  questions: string[];
  onQuestionClick?: (question: string) => void;
  disabled?: boolean;
}) {
  if (questions.length === 0) return null;

  return (
    <div data-testid="chat-info-questions-section">
      <SectionHeader icon={Lightbulb} title="Domande suggerite" />
      <div className="space-y-1.5">
        {questions.map(question => (
          <button
            key={question}
            onClick={() => onQuestionClick?.(question)}
            disabled={disabled || !onQuestionClick}
            className={cn(
              'w-full text-left px-3 py-2 rounded-lg text-xs transition-all duration-200',
              'flex items-start gap-2',
              disabled || !onQuestionClick
                ? 'bg-muted text-muted-foreground/50 cursor-not-allowed'
                : 'bg-card/50 text-foreground hover:bg-amber-50 dark:hover:bg-amber-500/10 hover:text-amber-900 dark:hover:text-amber-400 cursor-pointer border border-transparent hover:border-amber-200 dark:hover:border-amber-500/30'
            )}
            aria-label={`Ask: ${question}`}
          >
            <ChevronRight className="h-3 w-3 mt-0.5 flex-shrink-0 text-muted-foreground" />
            <span className="line-clamp-2">{question}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Panel Content (shared between desktop and mobile)
// ============================================================================

function PanelContent({
  game,
  agent,
  citations = [],
  suggestedQuestions = [],
  onCitationClick,
  onQuestionClick,
  isStreaming,
}: ChatInfoPanelProps) {
  return (
    <div className="flex flex-col gap-5 p-4" data-testid="chat-info-panel-content">
      {/* Game Card */}
      {game && <GameSection game={game} />}

      {/* Agent Card */}
      {agent && <AgentSection agent={agent} />}

      {/* Divider */}
      {(game || agent) && (citations.length > 0 || suggestedQuestions.length > 0) && (
        <hr className="border-border/50 dark:border-border/30" />
      )}

      {/* RAG Citations */}
      <CitationsSection citations={citations} onCitationClick={onCitationClick} />

      {/* Suggested Questions */}
      <SuggestedQuestionsSection
        questions={suggestedQuestions}
        onQuestionClick={onQuestionClick}
        disabled={isStreaming}
      />
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function ChatInfoPanel(props: ChatInfoPanelProps) {
  const { className } = props;
  const { isCollapsed, isMobileOpen, toggleCollapsed, setMobileOpen } = useChatInfoStore();
  const [isDesktop, setIsDesktop] = useState(false);

  // Track lg breakpoint (1024px)
  useEffect(() => {
    const mediaQuery = window.matchMedia('(min-width: 1024px)');
    setIsDesktop(mediaQuery.matches);

    const handleChange = (e: MediaQueryListEvent) => {
      setIsDesktop(e.matches);
    };

    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, []);

  // Desktop: fixed side panel
  if (isDesktop) {
    return (
      <>
        {/* Toggle button (always visible) */}
        <button
          onClick={toggleCollapsed}
          className={cn(
            'absolute top-4 z-10 p-1.5 rounded-lg transition-all duration-200',
            'bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50',
            'hover:bg-white dark:hover:bg-card text-muted-foreground hover:text-foreground',
            isCollapsed ? 'right-2' : 'right-[calc(theme(spacing.80)+0.5rem)]'
          )}
          aria-label={isCollapsed ? 'Apri pannello info' : 'Chiudi pannello info'}
          data-testid="chat-info-toggle"
        >
          {isCollapsed ? (
            <PanelRightOpen className="h-4 w-4" />
          ) : (
            <PanelRightClose className="h-4 w-4" />
          )}
        </button>

        {/* Panel */}
        <aside
          className={cn(
            'border-l border-border/50 dark:border-border/30',
            'bg-background/95 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none',
            'overflow-y-auto transition-all duration-300 ease-in-out flex-shrink-0',
            isCollapsed ? 'w-0 opacity-0 overflow-hidden' : PANEL_WIDTH,
            className
          )}
          aria-label="Chat info panel"
          data-testid="chat-info-panel"
        >
          {!isCollapsed && <PanelContent {...props} />}
        </aside>
      </>
    );
  }

  // Tablet/Mobile: Sheet drawer
  return (
    <>
      {/* Toggle button for mobile */}
      <button
        onClick={() => setMobileOpen(true)}
        className={cn(
          'fixed bottom-20 right-4 z-40 p-3 rounded-full',
          'bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50',
          'hover:bg-white dark:hover:bg-card text-muted-foreground hover:text-foreground',
          'shadow-lg transition-all duration-200'
        )}
        aria-label="Apri info chat"
        data-testid="chat-info-mobile-toggle"
      >
        <Info className="h-5 w-5" />
      </button>

      <Sheet open={isMobileOpen} onOpenChange={setMobileOpen}>
        <SheetContent
          side="right"
          className="w-80 max-w-[85vw] bg-background/95 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none p-0"
        >
          <SheetHeader className="px-4 pt-4 pb-2 border-b border-border/50 dark:border-border/30">
            <SheetTitle className="font-quicksand text-base">Info Chat</SheetTitle>
          </SheetHeader>
          <div className="overflow-y-auto h-[calc(100%-3.5rem)]">
            <PanelContent {...props} />
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}
