/**
 * AgentInfoCard - Tabbed Info Panel for Agent Detail Pages
 *
 * Companion card for MeepleCard (agent variant) showing:
 * - Tab 1: Chat interface with SSE streaming
 * - Tab 2: Chat history (previous sessions)
 * - Tab 3: Knowledge Base documents
 *
 * Follows MeepleInfoCard pattern for games.
 *
 * POC: Agent chat page with tabbed UI
 */

'use client';

import { useState, useEffect } from 'react';

import { MessageSquare, History, Book, FileText, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';


// Tab types
type TabType = 'chat' | 'history' | 'kb';

interface TabConfig {
  id: TabType;
  label: string;
  icon: typeof MessageSquare;
  color: string;
  bgColor: string;
}

const tabConfig: Record<TabType, TabConfig> = {
  chat: {
    id: 'chat',
    label: 'Chat',
    icon: MessageSquare,
    color: '#E67E22',
    bgColor: 'rgba(230, 126, 34, 0.08)',
  },
  history: {
    id: 'history',
    label: 'Storico',
    icon: History,
    color: '#3498DB',
    bgColor: 'rgba(52, 152, 219, 0.08)',
  },
  kb: {
    id: 'kb',
    label: 'Knowledge Base',
    icon: Book,
    color: '#9B59B6',
    bgColor: 'rgba(155, 89, 182, 0.08)',
  },
};

export interface AgentInfoCardProps {
  agentId: string;
  agentName: string;
  readOnly?: boolean;
  className?: string;
}

export function AgentInfoCard({
  agentId,
  agentName,
  readOnly = false,
  className,
}: AgentInfoCardProps) {
  const [activeTab, setActiveTab] = useState<TabType>('chat');
  const [documents, setDocuments] = useState<Array<{ id: string; name: string; url?: string }>>([]);
  const [docsLoading, setDocsLoading] = useState(false);
  const [docsError, setDocsError] = useState<string | null>(null);

  // Fetch agent documents
  useEffect(() => {
    if (activeTab === 'kb') {
      fetchDocuments();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, agentId]);

  const fetchDocuments = async () => {
    setDocsLoading(true);
    setDocsError(null);
    try {
      const result = await api.agents.getDocuments(agentId);
      setDocuments(result?.documents || []);
    } catch (err) {
      setDocsError(err instanceof Error ? err.message : 'Failed to load documents');
    } finally {
      setDocsLoading(false);
    }
  };

  const availableTabs: TabType[] = readOnly
    ? ['chat', 'kb']
    : ['chat', 'history', 'kb'];

  return (
    <div
      className={cn(
        'w-full max-w-[800px] flex-shrink-0 overflow-hidden rounded-3xl',
        'border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]',
        className,
      )}
      style={{
        minHeight: '600px',
        boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
      }}
      data-testid="agent-info-card"
    >
      {/* Tabs Header */}
      <div className="flex border-b border-[rgba(45,42,38,0.08)]">
        {availableTabs.map((tab) => {
          const config = tabConfig[tab];
          const Icon = config.icon;
          const isActive = activeTab === tab;

          return (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={cn(
                'flex flex-1 items-center justify-center gap-2 px-3 py-4',
                'font-quicksand text-sm font-semibold transition-all',
                isActive
                  ? 'border-b-2'
                  : 'text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]',
              )}
              style={
                isActive
                  ? {
                      borderBottomColor: config.color,
                      backgroundColor: config.bgColor,
                      color: config.color,
                    }
                  : undefined
              }
              data-testid={`agent-info-tab-${tab}`}
            >
              <Icon className="h-4 w-4" />
              <span className={availableTabs.length > 2 ? 'hidden sm:inline' : ''}>{config.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Content */}
      <div className="flex h-[calc(100%-57px)] flex-col">
        {/* Chat Tab - Redirects to unified chat system */}
        {activeTab === 'chat' && (
          <div className="flex flex-1 flex-col items-center justify-center text-center p-6">
            <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
              <MessageSquare className="h-8 w-8 text-[hsl(25,95%,38%)]" />
            </div>
            <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
              Chat AI
            </h4>
            <p className="max-w-xs font-nunito text-sm text-[#6B665C] mb-4">
              Usa il sistema di chat unificato per parlare con {agentName}
            </p>
            <Button
              variant="outline"
              size="sm"
              onClick={() => window.location.href = '/chat/new'}
              className="rounded-full"
            >
              Vai alla Chat
            </Button>
          </div>
        )}

        {/* History Tab */}
        {activeTab === 'history' && (
          <div className="flex flex-1 flex-col p-6">
            <h3 className="font-quicksand text-lg font-bold text-[#2D2A26] mb-4">
              Storico Conversazioni
            </h3>

            {/* TODO: Implement chat history list */}
            <div className="flex flex-1 flex-col items-center justify-center text-center">
              <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
                <History className="h-8 w-8 text-[hsl(25,95%,38%)]" />
              </div>
              <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
                Nessuna conversazione
              </h4>
              <p className="max-w-xs font-nunito text-sm text-[#6B665C]">
                Le tue conversazioni con {agentName} appariranno qui
              </p>
            </div>
          </div>
        )}

        {/* Knowledge Base Tab */}
        {activeTab === 'kb' && (
          <div className="flex flex-1 flex-col p-6 overflow-y-auto">
            <h3 className="font-quicksand text-lg font-bold text-[#2D2A26] mb-4">
              Documenti Knowledge Base
            </h3>

            {/* Loading state */}
            {docsLoading && documents.length === 0 && (
              <div className="flex flex-1 flex-col items-center justify-center text-center">
                <Loader2 className="mb-3 h-8 w-8 animate-spin text-[hsl(25,95%,38%)]" />
                <p className="font-nunito text-sm text-[#6B665C]">Caricamento documenti...</p>
              </div>
            )}

            {/* Error state */}
            {docsError && !docsLoading && (
              <div className="flex flex-1 flex-col items-center justify-center text-center">
                <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-red-50">
                  <FileText className="h-8 w-8 text-red-400" />
                </div>
                <p className="mb-3 font-nunito text-sm text-red-600">{docsError}</p>
                <Button variant="outline" size="sm" onClick={fetchDocuments} className="text-[#6B665C]">
                  Riprova
                </Button>
              </div>
            )}

            {/* Documents list */}
            {!docsLoading && !docsError && documents.length > 0 && (
              <div className="space-y-3">
                {documents.map((doc) => (
                  <div
                    key={doc.id}
                    className="flex items-center gap-3 rounded-xl bg-[rgba(45,42,38,0.04)] p-4 transition-colors hover:bg-[rgba(45,42,38,0.06)]"
                  >
                    <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg bg-white shadow-sm">
                      <FileText className="h-5 w-5 text-[hsl(25,95%,38%)]" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-nunito text-sm font-medium text-[#2D2A26]">
                        {doc.fileName}
                      </p>
                      <div className="flex items-center gap-2">
                        <span className="rounded bg-purple-100 px-1.5 py-0.5 text-xs font-medium text-purple-700">
                          {doc.documentType}
                        </span>
                        <span className="text-xs text-[#9C958A]">
                          {Math.round(doc.fileSizeBytes / 1024)} KB
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Empty state */}
            {!docsLoading && !docsError && documents.length === 0 && (
              <div className="flex flex-1 flex-col items-center justify-center text-center">
                <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
                  <Book className="h-8 w-8 text-[hsl(25,95%,38%)]" />
                </div>
                <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
                  Nessun documento
                </h4>
                <p className="mb-4 max-w-xs font-nunito text-sm text-[#6B665C]">
                  Questo agente non ha documenti assegnati alla knowledge base
                </p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
