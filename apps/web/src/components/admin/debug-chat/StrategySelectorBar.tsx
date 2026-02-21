'use client';

/**
 * StrategySelectorBar - Top bar with game selector + strategy dropdown + re-execute button
 *
 * Allows admin to select a game, choose a RAG strategy override, and re-execute the last query.
 */

import { useState, useEffect, useCallback } from 'react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { PanelRightIcon, PanelRightCloseIcon, RefreshCwIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

// Known RAG strategies (matching backend AgentStrategy value object)
const RAG_STRATEGIES = [
  { value: '', label: 'Default (auto)' },
  { value: 'HybridSearch', label: 'Hybrid Search' },
  { value: 'VectorOnly', label: 'Vector Only' },
  { value: 'KeywordOnly', label: 'Keyword Only' },
  { value: 'SingleModel', label: 'Single Model' },
  { value: 'IterativeRAG', label: 'Iterative RAG' },
  { value: 'MultiModel', label: 'Multi-Model' },
] as const;

interface GameOption {
  id: string;
  name: string;
}

interface StrategySelectorBarProps {
  selectedGameId: string;
  onGameChange: (gameId: string) => void;
  selectedStrategy: string;
  onStrategyChange: (strategy: string) => void;
  onReExecute: () => void;
  isStreaming: boolean;
  hasLastQuery: boolean;
  showDebug: boolean;
  onToggleDebug: () => void;
}

export function StrategySelectorBar({
  selectedGameId,
  onGameChange,
  selectedStrategy,
  onStrategyChange,
  onReExecute,
  isStreaming,
  hasLastQuery,
  showDebug,
  onToggleDebug,
}: StrategySelectorBarProps) {
  const [games, setGames] = useState<GameOption[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchGames = useCallback(async () => {
    try {
      const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const response = await fetch(`${baseUrl}/api/v1/games?pageSize=100`, {
        credentials: 'include',
      });
      if (!response.ok) throw new Error('Failed to fetch games');
      const data = await response.json();
      // Handle both paginated and flat array responses
      const items = data.items || data;
      if (Array.isArray(items)) {
        setGames(items.map((g: { id: string; name?: string; title?: string }) => ({
          id: g.id,
          name: g.name || g.title || g.id,
        })));
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
              <SelectItem key={s.value} value={s.value || '__default__'} className="text-xs">
                {s.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Re-execute button */}
      <button
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
  );
}
