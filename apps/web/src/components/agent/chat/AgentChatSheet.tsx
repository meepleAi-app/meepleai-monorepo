/**
 * Agent Chat Sheet - Bottom sheet chat container
 * Issue #3242 (FRONT-006)
 * Issue #3249: [FRONT-013] Agent Type Switcher & Dynamic Typology
 * Issue #3250: [FRONT-014] Agent Settings Drawer integration
 * Issue #3251: [FRONT-015] PDF Viewer integration
 *
 * Features:
 * - Bottom sheet with swipe gestures
 * - Header: game + agent type switcher + model + tokens
 * - SSE streaming ready
 * - Message list placeholder
 * - Resize handle
 * - Dynamic typology switching (preserves chat history)
 * - Settings drawer integration
 * - PDF viewer integration
 */

'use client';

import { useState, useCallback } from 'react';

import { X } from 'lucide-react';
import { toast } from 'sonner';

import { Sheet, SheetContent, SheetHeader } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useApprovedTypologies, useSwitchTypology } from '@/hooks/queries/useAgentTypologies';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';
import { useAgentStore } from '@/stores/agentStore';

import { AgentTypeSwitcher } from './AgentTypeSwitcher';
import { PdfViewerIntegration } from './PdfViewerIntegration';
import { AgentSettingsDrawer, type AgentRuntimeConfig } from '../settings/AgentSettingsDrawer';
import { ActionBar } from '../shared/ActionBar';


interface AgentChatSheetProps {
  /** Game title to display in header */
  gameTitle: string;
  /** Game ID for PDF viewer */
  gameId: string;
  /** Current typology (full object for switcher) */
  currentTypology: Typology;
  /** Model name to display */
  modelName: string;
  /** Tokens used in current session */
  tokensUsed: number;
  /** Token limit for session */
  tokensLimit: number;
  /** Session ID for typology switch API call */
  sessionId: string;
  /** Whether game has PDF documents */
  hasPdf?: boolean;
  /** User tier for settings */
  userTier?: 'free' | 'premium';
  /** Current agent config */
  currentConfig?: AgentRuntimeConfig;
  /** Callback when typology is switched successfully */
  onTypologySwitch?: (newTypology: Typology) => void;
  /** Callback when config is updated */
  onConfigUpdated?: (config: AgentRuntimeConfig) => void;
}

const DEFAULT_CONFIG: AgentRuntimeConfig = {
  modelId: 'gpt-3.5-turbo',
  temperature: 0.7,
  ragStrategy: 'hybrid',
  maxTokens: 2048,
  topK: 5,
  minScore: 0.7,
};

export function AgentChatSheet({
  gameTitle,
  gameId,
  currentTypology,
  modelName,
  tokensUsed,
  tokensLimit,
  sessionId,
  hasPdf = false,
  userTier = 'free',
  currentConfig = DEFAULT_CONFIG,
  onTypologySwitch,
  onConfigUpdated,
}: AgentChatSheetProps) {
  const { isChatOpen, closeChat, openPdfViewer } = useAgentStore();

  // Settings drawer state
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [agentConfig, setAgentConfig] = useState<AgentRuntimeConfig>(currentConfig);

  // Settings handlers
  const handleOpenSettings = useCallback(() => {
    setIsSettingsOpen(true);
  }, []);

  const handleCloseSettings = useCallback(() => {
    setIsSettingsOpen(false);
  }, []);

  const handleConfigUpdated = useCallback((config: AgentRuntimeConfig) => {
    setAgentConfig(config);
    onConfigUpdated?.(config);
  }, [onConfigUpdated]);

  // PDF handler
  const handleOpenPdf = useCallback(() => {
    // Open PDF at page 1 (or last viewed page from store)
    openPdfViewer(gameId, 1);
    toast.info(`📄 Apertura regolamento`, {
      description: 'Premi P per aprire/chiudere',
    });
  }, [gameId, openPdfViewer]);

  // Fetch approved typologies for switcher dropdown
  const { data: availableTypologies = [], isLoading: isLoadingTypologies } = useApprovedTypologies(isChatOpen);

  // Mutation for switching typology
  const { mutate: switchTypology, isPending: isSwitching } = useSwitchTypology();

  // Handle typology switch
  const handleTypologySwitch = (typologyId: string) => {
    switchTypology(
      { sessionId, typologyId },
      {
        onSuccess: (response) => {
          toast.success('Tipo agente aggiornato!', {
            description: `Ora stai usando: ${response.newTypologyName}`,
          });
          // Find the full typology object and notify parent
          const newTypology = availableTypologies.find((t) => t.id === typologyId);
          if (newTypology && onTypologySwitch) {
            onTypologySwitch(newTypology);
          }
        },
        onError: (error) => {
          toast.error('Errore nel cambio tipo agente', {
            description: error.message || 'Riprova più tardi',
          });
        },
      }
    );
  };

  return (
    <Sheet open={isChatOpen} onOpenChange={open => !open && closeChat()}>
      <SheetContent
        side="bottom"
        className="h-[85vh] flex flex-col"
      >
        {/* Header */}
        <SheetHeader className="border-b border-slate-800 pb-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              {/* Status indicator */}
              <div className="h-2 w-2 rounded-full bg-cyan-400 agent-pulse-cyan shrink-0" />

              {/* Game title */}
              <div className="flex flex-col gap-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <h3 className="font-semibold text-white text-sm">
                    {gameTitle}
                  </h3>
                  <span className="text-slate-500">—</span>

                  {/* Agent Type Switcher (Issue #3249) */}
                  {!isLoadingTypologies && availableTypologies.length > 0 && (
                    <AgentTypeSwitcher
                      currentTypology={currentTypology}
                      availableTypologies={availableTypologies}
                      onSwitch={handleTypologySwitch}
                      isLoading={isSwitching}
                    />
                  )}
                </div>

                {/* Model and tokens info */}
                <div className="flex items-center gap-2 text-xs text-slate-400">
                  <span className="px-2 py-0.5 rounded-full bg-purple-500/20 text-purple-300">
                    ⚡ {modelName}
                  </span>
                  <span>
                    {tokensUsed.toLocaleString()}/{tokensLimit.toLocaleString()} tokens
                  </span>
                </div>
              </div>
            </div>

            <Button variant="ghost" size="icon" onClick={closeChat} className="shrink-0">
              <X className="h-4 w-4" />
            </Button>
          </div>

          {/* Resize Handle */}
          <div className="absolute left-1/2 top-2 -translate-x-1/2">
            <div className="w-12 h-1 rounded-full bg-slate-700" />
          </div>
        </SheetHeader>

        {/* Chat Messages - Placeholder for #3243 */}
        <div className="flex-1 overflow-y-auto p-4">
          <div className="flex items-center justify-center h-full text-slate-400">
            <p className="text-sm">
              Message components from #3243 (FRONT-007)
            </p>
          </div>
        </div>

        {/* Action Bar */}
        <div className="border-t border-slate-800 pt-3">
          <ActionBar
            state="chat"
            onSettings={handleOpenSettings}
            onExport={() => {}}
            onMinimize={closeChat}
            onOpenPdf={handleOpenPdf}
            hasPdf={hasPdf}
          />
        </div>
      </SheetContent>

      {/* Settings Drawer - Issue #3250 */}
      <AgentSettingsDrawer
        isOpen={isSettingsOpen}
        onClose={handleCloseSettings}
        sessionId={sessionId}
        currentConfig={agentConfig}
        userTier={userTier}
        onConfigUpdated={handleConfigUpdated}
      />

      {/* PDF Viewer Integration - Issue #3251 */}
      <PdfViewerIntegration gameId={gameId} />
    </Sheet>
  );
}
