/**
 * GameChatTabV2 — orchestrator del chat-in-game V2.
 *
 * Compone gli 11 sotto-componenti del barrel game-chat. Wraps useGameChat.
 * Renderizza FSM in base ai campi della response:
 *   - isLowQuality === true → LowConfidenceDisclaimer (banner giallo)
 *   - outOfContext === true → OutOfContextActions (3 action pill)
 *   - sempre: ConfidenceBadge (3-tier color) e CitationChip[] per ogni citation
 *
 * Hybrid C citation behavior (Q4): tap chip → CitationModal preview.
 * Footer "Apri nella KB" attualmente NASCOSTO (onOpenInKb=undefined).
 * Quando G4 atterra: passa onOpenInKb={() => router.push(`/kb/${gameId}#page-${pageNumber}`)}.
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.1 §4
 */
'use client';

import { useEffect, useRef, useState, type ReactElement, type ReactNode } from 'react';

import type { Citation } from '@/types';

import { ChatBubble } from './ChatBubble';
import { ChatBubbleSkeleton } from './ChatBubbleSkeleton';
import { ChatHistoryBanner } from './ChatHistoryBanner';
import { ChatInputBar } from './ChatInputBar';
import { CitationChip } from './CitationChip';
import { CitationModal } from './CitationModal';
import { ConfidenceBadge } from './ConfidenceBadge';
import { GameChatHeader, type AgentKind } from './GameChatHeader';
import { GameChatSidebar } from './GameChatSidebar';
import {
  LowConfidenceDisclaimer,
  type DisclaimerAlternative,
} from './LowConfidenceDisclaimer';
import {
  OutOfContextActions,
  type OutOfContextAction,
} from './OutOfContextActions';
import { SuggestedPrompts, type SuggestedPrompt } from './SuggestedPrompts';
import { TypingIndicator } from './TypingIndicator';

import { useGameChat, type ChatMessage } from '@/hooks/queries/useGameChat';

export interface GameChatTabV2Props {
  readonly gameId: string;
  readonly initialAgent?: AgentKind;
  readonly gameTitle?: string;
  readonly gameIcon?: string;
}

const AGENT_AVATAR: Record<AgentKind, string> = { tutor: '🧙', arbitro: '⚖️' };
const AGENT_NAME: Record<AgentKind, string> = { tutor: 'Tutor', arbitro: 'Arbitro' };

const SUGGESTED_DEFAULT: ReadonlyArray<Omit<SuggestedPrompt, 'onClick'>> = [
  { category: 'A', text: 'Posso ripetere questa azione nel turno?' },
  { category: 'C', text: 'Setup per N giocatori?' },
  { category: 'E', text: 'Come si calcola il punteggio?' },
  { category: 'F', text: 'Edge case raro' },
];

export function GameChatTabV2({
  gameId,
  initialAgent = 'tutor',
  gameTitle = 'Gioco',
  gameIcon = '🎲',
}: GameChatTabV2Props): ReactElement {
  const chat = useGameChat(gameId, initialAgent);
  const [inputValue, setInputValue] = useState('');
  const [selectedCitation, setSelectedCitation] = useState<Citation | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // G2: auto-scroll to bottom on hydrate complete + new message
  useEffect(() => {
    if (chat.isHydrating) return;
    messagesEndRef.current?.scrollIntoView({ behavior: 'instant' as ScrollBehavior, block: 'end' });
  }, [chat.isHydrating, chat.messages.length]);

  const handleSubmit = (q: string) => {
    setInputValue('');
    void chat.ask(q);
  };

  const handleSuggestedClick = (text: string) => {
    setInputValue('');
    void chat.ask(text);
  };

  const renderAgentExtras = (msg: ChatMessage): ReactNode => {
    const showOoc = msg.outOfContext === true;
    const oocActions: OutOfContextAction[] = showOoc
      ? [
          { kind: 'switch-game', label: 'Cambia gioco', onClick: () => {} },
          { kind: 'find-agent', label: 'Cerca un agente', onClick: () => {} },
          { kind: 'stay', label: 'Resta qui', onClick: () => {} },
        ]
      : [];

    const lowConfAlts: DisclaimerAlternative[] = msg.isLowQuality && !showOoc
      ? [
          { label: 'Verifica nel manuale', kind: 'kb' },
          { label: 'Cerca su BGG', kind: 'external', url: 'https://boardgamegeek.com/' },
        ]
      : [];

    return (
      <>
        {msg.citations && msg.citations.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2 border-t border-dashed border-[hsl(var(--c-agent)/0.25)] pt-3">
            {msg.citations.map((c, i) => {
              // Citation type (domain.ts:134) ha pageNumber + snippet ma NO sectionTitle.
              // Usiamo il primo segmento dello snippet (max 60 char) come "section title".
              const previewTitle = c.snippet
                ? c.snippet.length > 60 ? c.snippet.slice(0, 57) + '…' : c.snippet
                : `Documento ${i + 1}`;
              return (
                <CitationChip
                  key={`${c.documentId}-${c.pageNumber}-${i}`}
                  pageNumber={c.pageNumber}
                  sectionTitle={previewTitle}
                  snippet={c.snippet}
                  onClick={() => setSelectedCitation(c)}
                />
              );
            })}
          </div>
        )}
        {msg.isLowQuality && !showOoc && (
          // Il disclaimer prefissa già "⚠️ Non sono certo." nel suo <strong>,
          // quindi rimuoviamo un eventuale prefisso "non sono certo[, .]" dal
          // content per evitare duplicazione testuale (e collisioni getByText).
          <LowConfidenceDisclaimer
            summary={msg.content.replace(/^\s*non sono certo[\s,.\-—]*/i, '')}
            alternatives={lowConfAlts}
          />
        )}
        {showOoc && <OutOfContextActions actions={oocActions} />}
        {msg.overallConfidence !== undefined && (
          <div className="mt-2 flex items-center justify-between gap-2 flex-wrap">
            <ConfidenceBadge score={msg.overallConfidence} />
          </div>
        )}
      </>
    );
  };

  const suggested: SuggestedPrompt[] = SUGGESTED_DEFAULT.map(p => ({
    ...p,
    onClick: () => handleSuggestedClick(p.text),
  }));

  return (
    <div className="flex h-full flex-col">
      <GameChatHeader
        gameTitle={gameTitle}
        gameIcon={gameIcon}
        agent={chat.currentAgent}
      />

      <div className="flex flex-1 overflow-hidden">
        {/* Desktop sidebar — hidden under lg */}
        <div className="hidden lg:block">
          <GameChatSidebar
            gameTitle={gameTitle}
            gameIcon={gameIcon}
            currentAgent={chat.currentAgent}
            history={[]}
            onAgentChange={chat.switchAgent}
            onHistorySelect={() => {}}
          />
        </div>

        <div className="flex flex-1 flex-col overflow-hidden">
          <div role="log" aria-live="polite" className="flex flex-1 flex-col gap-4 overflow-y-auto p-4">
            {chat.isHydrating ? (
              <ChatBubbleSkeleton count={3} />
            ) : (
              <>
                {chat.hasHistoricalMessages && chat.messages.some(m => !m.isHistorical) && (
                  <ChatHistoryBanner />
                )}
                {chat.messages.map(msg =>
                  msg.role === 'user' ? (
                    <ChatBubble
                      key={msg.id}
                      role="user"
                      content={msg.content}
                      isHistorical={msg.isHistorical}
                    />
                  ) : (
                    <ChatBubble
                      key={msg.id}
                      role="agent"
                      // Quando isLowQuality === true, il content della risposta è
                      // mostrato dentro il LowConfidenceDisclaimer come summary —
                      // sopprimiamo qui per evitare duplicazione UX.
                      content={msg.isLowQuality ? '' : msg.content}
                      agentName={AGENT_NAME[chat.currentAgent]}
                      avatar={AGENT_AVATAR[chat.currentAgent]}
                      isHistorical={msg.isHistorical}
                    >
                      {renderAgentExtras(msg)}
                    </ChatBubble>
                  )
                )}
                {chat.isLoading && (
                  <ChatBubble
                    role="agent"
                    content=""
                    agentName={AGENT_NAME[chat.currentAgent]}
                    avatar={AGENT_AVATAR[chat.currentAgent]}
                  >
                    <TypingIndicator hint="Cerco nella KB" />
                  </ChatBubble>
                )}
                <div ref={messagesEndRef} aria-hidden="true" />
              </>
            )}
          </div>
          <SuggestedPrompts prompts={suggested} groupLabel="Domande comuni" />
          <ChatInputBar
            value={inputValue}
            onChange={setInputValue}
            onSubmit={handleSubmit}
            disabled={chat.isLoading}
            placeholder={`Scrivi una domanda al ${AGENT_NAME[chat.currentAgent]}…`}
          />
        </div>
      </div>

      <CitationModal
        citation={selectedCitation}
        open={selectedCitation !== null}
        onClose={() => setSelectedCitation(null)}
        gameId={gameId}
      />
    </div>
  );
}
