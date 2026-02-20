/**
 * CardAgentAction
 *
 * Renders a prominent agent action button in the MeepleCard footer area.
 * Shows "Crea Agente" for games without an agent, or "Chat" for games with one.
 * Respects agent slot availability via useHasAvailableSlots().
 *
 * @see Issue #4777 - MeepleCard QuickAction: Crea Agente Button
 */

'use client';

import Link from 'next/link';

import { Bot, MessageCircle } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { useHasAvailableSlots } from '@/hooks/queries/useAgentSlots';
import { cn } from '@/lib/utils';

interface CardAgentActionProps {
  /** Whether this game already has an agent */
  hasAgent: boolean;
  /** Agent ID (required when hasAgent is true, for chat navigation) */
  agentId?: string;
  /** Game ID (for chat navigation) */
  gameId: string;
  /** Callback to open agent creation wizard */
  onCreateAgent?: () => void;
  /** Card variant affects sizing */
  variant?: 'grid' | 'list' | 'compact' | 'featured' | 'hero';
  /** Whether there's a navigation footer below (affects border-radius) */
  hasNavFooter?: boolean;
  className?: string;
}

export function CardAgentAction({
  hasAgent,
  agentId,
  gameId,
  onCreateAgent,
  variant = 'grid',
  hasNavFooter = false,
  className,
}: CardAgentActionProps) {
  const { hasAvailableSlots, isLoading } = useHasAvailableSlots();

  // Don't render for compact variant (too small)
  if (variant === 'compact') return null;

  // Agent exists → show "Chat" link
  if (hasAgent) {
    const chatHref = agentId
      ? `/chat/new?agentId=${agentId}`
      : `/chat/new?gameId=${gameId}`;

    return (
      <div
        className={cn(
          'flex items-center justify-center',
          'px-3 py-2',
          'border-t border-border/60',
          'bg-muted/[0.28] dark:bg-muted/20',
          !hasNavFooter && 'rounded-b-2xl',
          className,
        )}
        data-testid="card-agent-action"
      >
        <Link
          href={chatHref}
          className={cn(
            'flex items-center justify-center gap-2',
            'w-full px-3 py-1.5 rounded-lg',
            'text-xs font-semibold font-nunito',
            'bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300',
            'hover:bg-blue-100 dark:hover:bg-blue-950/60',
            'transition-colors duration-200',
          )}
          onClick={(e) => e.stopPropagation()}
          data-testid="card-agent-chat-link"
        >
          <MessageCircle className="w-3.5 h-3.5" />
          <span>Chat</span>
        </Link>
      </div>
    );
  }

  // No agent → show "Crea Agente" button
  const isDisabled = !hasAvailableSlots || isLoading || !onCreateAgent;

  const button = (
    <button
      onClick={(e) => {
        e.stopPropagation();
        onCreateAgent?.();
      }}
      disabled={isDisabled}
      className={cn(
        'flex items-center justify-center gap-2',
        'w-full px-3 py-1.5 rounded-lg',
        'text-xs font-semibold font-nunito',
        'transition-colors duration-200',
        isDisabled
          ? 'bg-muted/60 text-muted-foreground/50 cursor-not-allowed'
          : [
              'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
              'hover:bg-amber-100 dark:hover:bg-amber-950/60',
            ],
      )}
      data-testid="card-agent-create-button"
    >
      <Bot className="w-3.5 h-3.5" />
      <span>Crea Agente</span>
    </button>
  );

  return (
    <div
      className={cn(
        'flex items-center justify-center',
        'px-3 py-2',
        'border-t border-border/60',
        'bg-muted/[0.28] dark:bg-muted/20',
        !hasNavFooter && 'rounded-b-2xl',
        className,
      )}
      data-testid="card-agent-action"
    >
      {isDisabled && !isLoading ? (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>{button}</TooltipTrigger>
            <TooltipContent>
              <p>Nessuno slot disponibile</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      ) : (
        button
      )}
    </div>
  );
}
