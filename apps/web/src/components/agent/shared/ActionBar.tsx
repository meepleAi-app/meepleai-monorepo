/**
 * Contextual Action Bar - State-aware action buttons
 * Issue #3241 (FRONT-005)
 * Issue #3251 (FRONT-015) - Added PDF button for chat state
 */

'use client';

import { Settings, Download, Minimize2, BarChart, FileText } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { useAgentStore } from '@/stores/agentStore';

type ActionBarState = 'config' | 'chat' | 'slots';

interface ActionBarProps {
  state: ActionBarState;
  onLaunch?: () => void;
  onCancel?: () => void;
  onSettings?: () => void;
  onExport?: () => void;
  onMinimize?: () => void;
  onViewUsage?: () => void;
  onOpenPdf?: () => void;
  disabled?: boolean;
  /** Whether PDF is available for this game */
  hasPdf?: boolean;
}

export function ActionBar({
  state,
  onLaunch,
  onCancel,
  onSettings,
  onExport,
  onMinimize,
  onViewUsage,
  onOpenPdf,
  disabled = false,
  hasPdf = false,
}: ActionBarProps) {
  const { selectedGameId, selectedTypologyId, selectedModelId } = useAgentStore();

  const isConfigComplete = selectedGameId && selectedTypologyId && selectedModelId;

  if (state === 'config') {
    return (
      <div className="flex gap-3 pb-safe">
        <Button variant="ghost" onClick={onCancel} className="flex-1">
          Cancel
        </Button>
        <Button
          onClick={onLaunch}
          disabled={!isConfigComplete || disabled}
          className="flex-1 bg-cyan-500 hover:bg-cyan-600 text-black font-bold agent-pulse-cyan disabled:opacity-50 disabled:animate-none"
        >
          🚀 Launch Agent
        </Button>
      </div>
    );
  }

  if (state === 'chat') {
    return (
      <TooltipProvider>
        <div className="flex gap-2 pb-safe">
          {/* PDF Button - Issue #3251 */}
          {hasPdf && (
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="ghost" size="icon" onClick={onOpenPdf}>
                  <FileText className="h-4 w-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                <p>Apri Regolamento (P)</p>
              </TooltipContent>
            </Tooltip>
          )}

          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon" onClick={onSettings}>
                <Settings className="h-4 w-4" />
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Impostazioni</p>
            </TooltipContent>
          </Tooltip>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon" onClick={onExport}>
                <Download className="h-4 w-4" />
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Esporta Chat</p>
            </TooltipContent>
          </Tooltip>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon" onClick={onMinimize} className="ml-auto">
                <Minimize2 className="h-4 w-4" />
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Minimizza</p>
            </TooltipContent>
          </Tooltip>
        </div>
      </TooltipProvider>
    );
  }

  if (state === 'slots') {
    return (
      <div className="flex gap-3 pb-safe">
        <Button variant="ghost" onClick={onViewUsage} className="flex-1">
          <BarChart className="h-4 w-4 mr-2" />
          View Usage
        </Button>
        <Button onClick={onCancel} className="flex-1">
          Back
        </Button>
      </div>
    );
  }

  return null;
}
