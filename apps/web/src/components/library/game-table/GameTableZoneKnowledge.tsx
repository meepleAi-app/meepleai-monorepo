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

import { FileText, MessageSquare, Activity, ChevronRight, Upload } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AgentStatusBadge } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import type { AgentStatus } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { KbStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { Button } from '@/components/ui/primitives/button';
import { useAgentKbDocs, useAgentThreads } from '@/hooks/queries/useAgentData';
import { useGameAgents } from '@/hooks/queries/useGameAgents';
import { useAgentStatus } from '@/hooks/useAgentStatus';
import { useGameTableDrawer } from '@/lib/stores/gameTableDrawerStore';

// ============================================================================
// Types
// ============================================================================

export interface GameTableZoneKnowledgeProps {
  gameId: string;
  /** @deprecated Use gameId — the agent is now resolved automatically via useGameAgents */
  agentId?: string;
}

// ============================================================================
// Styling constants
// ============================================================================

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

// ============================================================================
// Helpers
// ============================================================================

/**
 * Map useAgentStatus ragStatus string to AgentStatusBadge status.
 */
function mapRagStatusToBadge(ragStatus: string | undefined, isReady: boolean): AgentStatus {
  if (!ragStatus) return 'idle';
  const lower = ragStatus.toLowerCase();
  if (lower === 'ready' && isReady) return 'active';
  if (lower === 'processing' || lower === 'training') return 'training';
  if (lower === 'error' || lower === 'failed') return 'error';
  return 'idle';
}

// ============================================================================
// Component
// ============================================================================

export function GameTableZoneKnowledge({ gameId }: GameTableZoneKnowledgeProps): React.ReactNode {
  const router = useRouter();

  // Resolve the actual agent ID for this game
  const { data: agents = [] } = useGameAgents({ gameId });
  const resolvedAgentId = agents.length > 0 ? agents[0].id : null;

  const { data: docs = [], isLoading: docsLoading } = useAgentKbDocs(gameId);
  const { data: threads = [], isLoading: threadsLoading } = useAgentThreads(resolvedAgentId ?? '');
  const { status: agentStatus, isLoading: statusLoading } = useAgentStatus(resolvedAgentId ?? '');
  const drawerOpen = useGameTableDrawer(s => s.open);

  const lastThread = resolvedAgentId && threads.length > 0 ? threads[0] : null;

  return (
    <div className="space-y-3">
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

      {/* Chat preview — only when an agent exists */}
      {resolvedAgentId && (
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
                onClick={() => drawerOpen({ type: 'chat', agentId: resolvedAgentId })}
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
                onClick={() => drawerOpen({ type: 'chat', agentId: resolvedAgentId })}
                data-testid="open-chat-btn"
              >
                Inizia chat
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </div>
      )}

      {/* Agent status — only when an agent exists */}
      {resolvedAgentId && (
        <div className={`${CARD_ROW} flex items-center gap-3`} data-testid="agent-status-section">
          <Activity className="h-4 w-4 text-amber-400 shrink-0" />
          <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">Agente</span>
          <div className="ml-auto">
            {statusLoading ? (
              <div
                className="h-5 w-16 bg-[#30363d] rounded animate-pulse"
                data-testid="status-skeleton"
              />
            ) : (
              <AgentStatusBadge
                status={mapRagStatusToBadge(agentStatus?.ragStatus, agentStatus?.isReady ?? false)}
                showLabel
                size="sm"
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
}
