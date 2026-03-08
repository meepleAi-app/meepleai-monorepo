/**
 * AgentDrawerSheet - Agent Drawer for Library Cards
 *
 * Right-side Sheet that displays agents for a game.
 * Shows single agent detail or agent list (when multiple).
 * Triggered by clicking "Agents" in CardNavigationFooter.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import { Bot, Loader2, Settings, Zap } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface AgentDrawerSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  gameTitle: string;
}

export function AgentDrawerSheet({ open, onOpenChange, gameId, gameTitle }: AgentDrawerSheetProps) {
  const {
    data: agents = [],
    isLoading: loading,
    error,
  } = useQuery({
    queryKey: ['game-agents', gameId],
    queryFn: () => api.agents.getUserAgentsForGame(gameId),
    enabled: open && !!gameId,
    staleTime: 2 * 60 * 1000,
  });

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className={cn('sm:max-w-lg', 'bg-white/80 backdrop-blur-xl')}>
        <SheetHeader className="pb-4 border-b border-border/30">
          <SheetTitle className="font-quicksand text-lg">
            <span className="text-[hsl(220,70%,55%)]">Agents</span>{' '}
            <span className="text-card-foreground">{gameTitle}</span>
          </SheetTitle>
          <div className="flex items-center gap-3 mt-1">
            <span className="text-xs text-muted-foreground font-nunito">
              {agents.length} {agents.length === 1 ? 'agente' : 'agenti'}
            </span>
            <div className="flex-1" />
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                onOpenChange(false);
                window.location.href = `/library/games/${gameId}/agent`;
              }}
              className="h-7 gap-1.5 text-xs font-nunito"
            >
              <Settings className="h-3.5 w-3.5" />
              Configura
            </Button>
          </div>
        </SheetHeader>

        <div className="mt-4 flex flex-col gap-3 overflow-y-auto max-h-[calc(100vh-180px)]">
          {/* Loading */}
          {loading && agents.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-2">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              <p className="text-sm text-muted-foreground font-nunito">Caricamento...</p>
            </div>
          )}

          {/* Error */}
          {error && (
            <div className="flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-700">
              Errore nel caricamento degli agenti
            </div>
          )}

          {/* Empty state */}
          {!loading && !error && agents.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-3 text-center">
              <div className="rounded-full bg-muted/50 p-4">
                <Bot className="h-8 w-8 text-muted-foreground" />
              </div>
              <div>
                <p className="font-quicksand font-semibold text-card-foreground">
                  Nessun agente configurato
                </p>
                <p className="text-sm text-muted-foreground font-nunito mt-1">
                  Configura un agente AI per questo gioco
                </p>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  onOpenChange(false);
                  window.location.href = `/library/games/${gameId}/agent`;
                }}
                className="mt-2 gap-1.5"
              >
                <Settings className="h-4 w-4" />
                Configura Agente
              </Button>
            </div>
          )}

          {/* Agent list */}
          {agents.map(agent => (
            <div
              key={agent.id}
              className={cn(
                'flex items-center gap-3 rounded-xl p-3',
                'bg-[rgba(45,42,38,0.04)] hover:bg-[rgba(45,42,38,0.06)]',
                'transition-colors'
              )}
            >
              <span
                className={cn(
                  'h-2.5 w-2.5 rounded-full flex-shrink-0',
                  agent.isActive ? 'bg-green-500' : 'bg-slate-300'
                )}
              />
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-card-foreground truncate font-nunito">
                  {agent.name}
                </p>
                <div className="flex items-center gap-2 mt-0.5">
                  <span className="text-xs text-muted-foreground">{agent.strategyName}</span>
                  {agent.invocationCount > 0 && (
                    <span className="flex items-center gap-0.5 text-xs text-muted-foreground">
                      <Zap className="h-3 w-3" />
                      {agent.invocationCount}
                    </span>
                  )}
                </div>
              </div>

              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  onOpenChange(false);
                  window.location.href = `/chat/new?game=${gameId}&agent=${agent.id}`;
                }}
                className="h-8 gap-1.5 text-xs"
              >
                <Bot className="h-3.5 w-3.5" />
                Chat
              </Button>
            </div>
          ))}
        </div>
      </SheetContent>
    </Sheet>
  );
}
