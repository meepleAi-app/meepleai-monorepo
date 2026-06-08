/**
 * ChatInfoPanel - Right sidebar panel for ChatThreadView (desktop only)
 *
 * Shows:
 * - Agent meta (name + type, F11 #1974 polish)
 * - Game info (linked game title)
 * - Citations from assistant messages
 * - Suggested follow-up questions
 *
 * F11 #1974 (audit 2026-06-07) polish:
 * - Hardcoded IT strings ("Gioco collegato", "Citazioni (N)", "Domande
 *   suggerite") moved to `chat.infoPanel.*` i18n keys (it.json + en.json).
 * - Optional `agent` section added at the top — renders only when the
 *   parent passes an agent prop, so legacy consumers (no agent context)
 *   are unaffected.
 */

'use client';

import React from 'react';

import { useTranslation } from '@/hooks/useTranslation';
import type { Citation } from '@/types';

import { CitationBadge } from './CitationBadge';

// ============================================================================
// Types
// ============================================================================

export interface ChatInfoPanelAgentMeta {
  /** Display-ready name (e.g. "Tutor", "Auto (Orchestrator)"). */
  readonly name: string;
  /** Optional typology label (e.g. "tutor", "auto"). Surfaced for power users. */
  readonly typology?: string;
}

export interface ChatInfoPanelProps {
  game: { id: string; title: string } | null;
  citations: Citation[];
  suggestedQuestions: string[];
  onQuestionClick: (question: string) => void;
  /**
   * F11 #1974: optional agent meta block at the top of the panel. Hidden when
   * undefined so threads without an agent context still render cleanly.
   */
  agent?: ChatInfoPanelAgentMeta;
}

// ============================================================================
// Component
// ============================================================================

export function ChatInfoPanel({
  game,
  citations,
  suggestedQuestions,
  onQuestionClick,
  agent,
}: ChatInfoPanelProps) {
  const { t } = useTranslation();

  return (
    <div
      className="hidden lg:flex w-[340px] flex-shrink-0 flex-col border-l border-border/50 bg-card/50 p-4 overflow-y-auto"
      data-slot="chat-info-panel"
    >
      {agent && (
        <div className="mb-4" data-slot="chat-info-panel-agent">
          <h4 className="text-sm font-semibold font-quicksand mb-1">
            {t('chat.infoPanel.agentSectionHeading')}
          </h4>
          <p className="text-xs text-foreground">{agent.name}</p>
          {agent.typology && (
            <p className="mt-0.5 text-[10px] uppercase tracking-wide text-muted-foreground">
              {t('chat.infoPanel.agentTypeLabel')}: {agent.typology}
            </p>
          )}
        </div>
      )}
      {game && (
        <div className="mb-4" data-slot="chat-info-panel-game">
          <h4 className="text-sm font-semibold font-quicksand mb-1">{game.title}</h4>
          <p className="text-xs text-muted-foreground">{t('chat.infoPanel.linkedGameLabel')}</p>
        </div>
      )}
      {citations.length > 0 && (
        <div className="mb-4" data-slot="chat-info-panel-citations">
          <h4 className="text-sm font-semibold font-quicksand mb-2">
            {t('chat.infoPanel.citationsHeading', { count: citations.length })}
          </h4>
          <div className="flex flex-wrap gap-1">
            {citations.slice(0, 20).map((c, i) => (
              <CitationBadge key={`panel-${c.documentId}-${c.pageNumber}-${i}`} citation={c} />
            ))}
          </div>
        </div>
      )}
      {suggestedQuestions.length > 0 && (
        <div data-slot="chat-info-panel-suggestions">
          <h4 className="text-sm font-semibold font-quicksand mb-2">
            {t('chat.infoPanel.suggestedQuestionsHeading')}
          </h4>
          <div className="space-y-1">
            {suggestedQuestions.map((q, i) => (
              <button
                key={i}
                onClick={() => onQuestionClick(q)}
                className="block w-full text-left text-xs px-2 py-1.5 rounded hover:bg-muted/50 transition-colors text-muted-foreground hover:text-foreground"
              >
                {q}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
