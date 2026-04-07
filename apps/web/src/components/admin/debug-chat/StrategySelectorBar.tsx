'use client';

/**
 * StrategySelectorBar - Top bar with game selector, strategy dropdown,
 * model/temperature/topK overrides, re-execute button, and debug toggle.
 */

import { useState, useEffect, useCallback } from 'react';

import { PanelRightIcon, PanelRightCloseIcon, RefreshCwIcon } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { RAG_STRATEGIES } from '@/lib/constants/rag-strategies';
import { cn } from '@/lib/utils';

interface GameOption {
  id: string;
  name: string;
}

interface StrategySelectorBarProps {
  selectedGameId: string;
  onGameChange: (gameId: string) => void;
  selectedStrategy: string;
  onStrategyChange: (strategy: string) => void;
  selectedModel: string;
  onModelChange: (model: string) => void;
  temperature: number;
  onTemperatureChange: (temp: number) => void;
  topK: number;
  onTopKChange: (topK: number) => void;
  onReExecute: () => void;
  isStreaming: boolean;
  hasLastQuery: boolean;
  showDebug: boolean;
  onToggleDebug: () => void;
  availableModels: { id: string; displayName: string; modelIdentifier: string }[];
  modelsLoading: boolean;
}

export function StrategySelectorBar({
  selectedGameId,
  onGameChange,
  selectedStrategy,
  onStrategyChange,
  selectedModel,
  onModelChange,
  temperature,
  onTemperatureChange,
  topK,
  onTopKChange,
  onReExecute,
  isStreaming,
  hasLastQuery,
  showDebug,
  onToggleDebug,
  availableModels,
  modelsLoading,
}: StrategySelectorBarProps) {
  const [games, setGames] = useState<GameOption[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchGames = useCallback(async () => {
    try {
      const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const response = await fetch(`${baseUrl}/api/v1/admin/shared-games?page=1&pageSize=100`, {
        credentials: 'include',
      });
      if (!response.ok) throw new Error('Failed to fetch games');
      const data = await response.json();
      // Handle both paginated and flat array responses
      const items = data.items || data;
      if (Array.isArray(items)) {
        setGames(
          items.map((g: { id: string; name?: string; title?: string }) => ({
            id: g.id,
            name: g.title || g.name || g.id,
          }))
        );
      }
    } catch {
      // Silently fail - games list is a convenience
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  return (
    <>
      {/* Row 1: Game + Strategy + Re-execute + Debug toggle */}
      <div className="flex items-center gap-3 border-b px-4 py-2.5 bg-muted/30">
        {/* Game selector */}
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <label className="text-xs font-medium text-muted-foreground shrink-0">Game</label>
          <Select value={selectedGameId} onValueChange={onGameChange}>
            <SelectTrigger className="h-8 text-xs max-w-[240px]">
              <SelectValue placeholder={loading ? 'Loading...' : 'Select game'} />
            </SelectTrigger>
            <SelectContent>
              {games.map(game => (
                <SelectItem key={game.id} value={game.id} className="text-xs">
                  {game.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Strategy selector */}
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-muted-foreground shrink-0">Strategy</label>
          <Select value={selectedStrategy} onValueChange={onStrategyChange}>
            <SelectTrigger className="h-8 text-xs w-[160px]">
              <SelectValue placeholder="Default" />
            </SelectTrigger>
            <SelectContent>
              {RAG_STRATEGIES.map(s => (
                <SelectItem key={s.value} value={s.value} className="text-xs">
                  {s.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Re-execute button */}
        <button
          data-testid="playground-reexecute-btn"
          onClick={onReExecute}
          disabled={isStreaming || !hasLastQuery}
          className={cn(
            'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors',
            'bg-primary text-primary-foreground hover:bg-primary/90',
            'disabled:opacity-50 disabled:cursor-not-allowed',
            isStreaming && 'animate-pulse'
          )}
          type="button"
        >
          <RefreshCwIcon className={cn('h-3.5 w-3.5', isStreaming && 'animate-spin')} />
          Re-execute
        </button>

        {/* Debug panel toggle */}
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                data-testid="playground-debug-toggle"
                onClick={onToggleDebug}
                className={cn(
                  'inline-flex items-center justify-center rounded-md p-1.5 transition-colors',
                  showDebug
                    ? 'bg-primary/10 text-primary hover:bg-primary/20'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                )}
                type="button"
                aria-label={showDebug ? 'Nascondi pipeline debug' : 'Mostra pipeline debug'}
                aria-pressed={showDebug}
              >
                {showDebug ? (
                  <PanelRightCloseIcon className="h-4 w-4" />
                ) : (
                  <PanelRightIcon className="h-4 w-4" />
                )}
              </button>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              {showDebug ? 'Nascondi pipeline debug' : 'Mostra pipeline debug'}
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>

      {/* Row 2: Model + Temperature + TopK */}
      <div className="flex items-center gap-3 border-b px-4 py-2 bg-muted/20">
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-muted-foreground shrink-0">Model</label>
          <Select value={selectedModel} onValueChange={onModelChange} disabled={modelsLoading}>
            <SelectTrigger className="h-8 text-xs w-[200px]">
              <SelectValue placeholder={modelsLoading ? 'Loading...' : 'Default'} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__default__" className="text-xs">
                Default (agent config)
              </SelectItem>
              {availableModels.map(m => (
                <SelectItem key={m.id} value={m.modelIdentifier} className="text-xs">
                  {m.displayName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-muted-foreground shrink-0">Temp</label>
          <input
            type="number"
            value={temperature}
            onChange={e => onTemperatureChange(Number(e.target.value))}
            min={0}
            max={2}
            step={0.1}
            className="h-8 w-[70px] rounded-md border bg-background px-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-muted-foreground shrink-0">Top-K</label>
          <input
            type="number"
            value={topK}
            onChange={e => onTopKChange(Number(e.target.value))}
            min={1}
            max={20}
            className="h-8 w-[60px] rounded-md border bg-background px-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
      </div>
    </>
  );
}
