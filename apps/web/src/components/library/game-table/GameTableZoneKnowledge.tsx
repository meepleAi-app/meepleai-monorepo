/**
 * GameTableZoneKnowledge — Knowledge Base zone for the Game Table
 *
 * Renders KB documents list with upload button, chat preview with last thread,
 * and agent readiness status. Each section is a dark card row.
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import React from 'react';

import { FileText, MessageSquare, ChevronRight, Upload, BookOpen } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { DocumentSelectionPanel } from '@/components/library/DocumentSelectionPanel';
import { Badge } from '@/components/ui/data-display/badge';
// KbStatusBadge removed in MeepleCard rewrite — inline replacement.
// Issue #4138: a local AgentStatusBadge stood here too, rendering the agent status row that
// has gone with the per-agent status signal.
type KbStatus = 'indexed' | 'processing' | 'failed' | 'none';
function KbStatusBadge({ status }: { status: KbStatus; size?: string }) {
  const colors: Record<string, string> = {
    indexed: 'bg-green-100 text-green-700',
    processing: 'bg-blue-100 text-blue-700',
    failed: 'bg-red-100 text-red-700',
    none: 'bg-muted text-muted-foreground',
  };
  return (
    <span className={`rounded px-1.5 py-0.5 text-xs font-semibold ${colors[status] ?? ''}`}>
      {status}
    </span>
  );
}
import { Button } from '@/components/ui/primitives/button';
import { useAgentKbDocs, useGameThreads } from '@/hooks/queries/useAgentData';
import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';
import { useGameTableDrawer } from '@/lib/stores/game-table-drawer-store';

import { HouseRulesSection } from './HouseRulesSection';

// ============================================================================
// Types
// ============================================================================

export interface GameTableZoneKnowledgeProps {
  gameId: string;
}

// ============================================================================
// Styling constants
// ============================================================================

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

// ============================================================================
// Helpers
// ============================================================================

// ============================================================================
// Component
// ============================================================================

export function GameTableZoneKnowledge({ gameId }: GameTableZoneKnowledgeProps): React.ReactNode {
  const router = useRouter();

  // Issue #4138: this resolved `agents[0].id` from useGameAgents and gated three blocks on it.
  // All three were wrong in a different way:
  //   - the chat preview used useAgentThreads(agentId), and the endpoint IGNORES `agentId` — it
  //     binds `gameId`. So the "preview" showed the user's most recent thread across ALL games,
  //     labelled as this game's. It now uses useGameThreads.
  //   - the agent status block showed /agents/{id}/status, a GLOBAL signal once there is one
  //     system agent. The per-game signal is `kbStatus`, already rendered above.
  //   - the document panel takes `gameId` and never needed an agent at all: it was hidden
  //     behind a condition irrelevant to it.
  const { data: docs = [], isLoading: docsLoading } = useAgentKbDocs(gameId);
  const { data: threads = [], isLoading: threadsLoading } = useGameThreads(gameId);
  const drawerOpen = useGameTableDrawer(s => s.open);
  const kbStatus = useGameKbStatus(gameId);

  const lastThread = threads.length > 0 ? threads[0] : null;

  return (
    <div className="space-y-3">
      {/* KB Coverage Badge + Suggested Questions */}
      {kbStatus.isIndexed && (
        <div className={CARD_ROW} data-testid="kb-status-section">
          <div className="flex items-center gap-2 mb-2">
            <Badge
              variant="secondary"
              className="gap-1 text-teal-600 border-teal-200 bg-teal-50"
              data-testid="kb-coverage-badge"
            >
              <BookOpen className="h-3 w-3" />
              KB {kbStatus.coverageLevel}
            </Badge>
          </div>

          {kbStatus.suggestedQuestions.length > 0 && (
            <div className="mt-1">
              <p className="text-xs text-[#8b949e] font-nunito mb-2">Domande frequenti:</p>
              <div className="flex flex-wrap gap-2" data-testid="suggested-questions">
                {kbStatus.suggestedQuestions.map((q, i) => (
                  <button
                    key={i}
                    className="text-xs px-3 py-1 rounded-full border border-[#30363d] bg-[#30363d] text-[#e6edf3] hover:bg-[#444d56] transition-colors"
                    data-testid="suggested-question-btn"
                  >
                    {q}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* KB Documents */}
      <div className={CARD_ROW} data-testid="kb-docs-section">
        <div className="flex items-center gap-2 mb-2">
          <FileText className="h-4 w-4 text-amber-400" />
          <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">Documenti KB</span>
          <span className="ml-auto text-xs text-[#8b949e] font-nunito" data-testid="doc-count">
            {docsLoading ? '...' : docs.length}
          </span>
        </div>

        {docsLoading ? (
          <div className="space-y-2">
            {[1, 2].map(i => (
              <div
                key={i}
                className="h-6 bg-[#30363d] rounded animate-pulse"
                data-testid="doc-skeleton"
              />
            ))}
          </div>
        ) : docs.length === 0 ? (
          <p className="text-xs text-[#8b949e] font-nunito">Nessun documento caricato</p>
        ) : (
          <ul className="space-y-1.5">
            {docs.map(doc => (
              <li
                key={doc.id}
                className="flex items-center justify-between text-sm"
                data-testid="kb-doc-item"
              >
                <span className="text-[#e6edf3] font-nunito truncate mr-2">{doc.fileName}</span>
                <KbStatusBadge status={doc.status} size="sm" />
              </li>
            ))}
          </ul>
        )}

        {/* PDF Upload button */}
        <Button
          size="sm"
          variant="ghost"
          className="w-full justify-between text-amber-400 hover:text-amber-300 hover:bg-[#30363d] mt-2"
          onClick={() => router.push(`/library/${gameId}?action=upload-pdf`)}
          data-testid="upload-pdf-btn"
        >
          <span className="flex items-center gap-2">
            <Upload className="h-3.5 w-3.5" />
            Carica PDF
          </span>
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>

      {/* House Rules */}
      <HouseRulesSection gameId={gameId} />

      {/* Chat preview — threads of this game */}
      <div className={CARD_ROW} data-testid="chat-preview-section">
        <div className="flex items-center gap-2 mb-2">
          <MessageSquare className="h-4 w-4 text-amber-400" />
          <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">Chat</span>
        </div>

        {threadsLoading ? (
          <div className="h-6 bg-[#30363d] rounded animate-pulse" data-testid="chat-skeleton" />
        ) : lastThread ? (
          <div className="space-y-2">
            <p
              className="text-xs text-[#8b949e] font-nunito truncate"
              data-testid="last-thread-preview"
            >
              {lastThread.firstMessagePreview || 'Conversazione recente'}
            </p>
            <Button
              size="sm"
              variant="ghost"
              className="w-full justify-between text-amber-400 hover:text-amber-300 hover:bg-[#30363d]"
              onClick={() => drawerOpen({ type: 'chat', gameId })}
              data-testid="open-chat-btn"
            >
              Apri chat
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        ) : (
          <div className="space-y-2">
            <p className="text-xs text-[#8b949e] font-nunito">Nessuna conversazione</p>
            <Button
              size="sm"
              variant="ghost"
              className="w-full justify-between text-amber-400 hover:text-amber-300 hover:bg-[#30363d]"
              onClick={() => drawerOpen({ type: 'chat', gameId })}
              data-testid="open-chat-btn"
            >
              Inizia chat
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        )}
      </div>

      {/* Issue #4138: an "Agente" status row stood here, from useAgentStatus(agents[0].id).
          With one system agent that status is global, not a property of this game: the
          per-game readiness is `kbStatus`, rendered in the KB block above. */}

      {/* Document selection — per game, never needed an agent */}
      <details className="mt-3">
        <summary className="text-xs font-semibold text-muted-foreground uppercase tracking-wider cursor-pointer hover:text-foreground select-none">
          Gestisci documenti agente
        </summary>
        <div className="mt-2 bg-[#21262d] rounded-lg border border-[#30363d] p-3">
          <DocumentSelectionPanel gameId={gameId} />
        </div>
      </details>
    </div>
  );
}
