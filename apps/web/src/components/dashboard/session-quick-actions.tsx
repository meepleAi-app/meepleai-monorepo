'use client';

import { useRouter } from 'next/navigation';

import { SESSION_QUICK_ACTIONS } from '@/config/entity-actions';
import { useCardHand } from '@/stores/use-card-hand';

interface SessionQuickActionsProps {
  gameId: string;
  sessionId: string;
}

export function SessionQuickActions({ gameId, sessionId: _sessionId }: SessionQuickActionsProps) {
  const { drawCard } = useCardHand();
  const router = useRouter();

  function handleTap(actionId: string, route?: string) {
    if (actionId === 'ask-ai') {
      drawCard({
        id: `agent-${gameId}`,
        entity: 'agent',
        title: 'AI Assistant',
        href: `/agents/${gameId}`,
      });
      router.push(`/agents/${gameId}`);
      return;
    }
    if (route) {
      const label = SESSION_QUICK_ACTIONS.find(a => a.id === actionId)?.label ?? actionId;
      drawCard({
        id: `${actionId}-${gameId}`,
        entity: 'kb',
        title: label,
        href: `/games/${gameId}${route}`,
      });
      router.push(`/games/${gameId}${route}`);
    }
  }

  return (
    <div className="grid grid-cols-4 gap-2" data-testid="session-quick-actions">
      {SESSION_QUICK_ACTIONS.map(action => {
        const Icon = action.icon;
        return (
          <button
            key={action.id}
            type="button"
            onClick={() => handleTap(action.id, action.route)}
            className="flex flex-col items-center gap-1.5 rounded-xl border border-border/30 bg-card/60 backdrop-blur-sm p-3 transition-all hover:shadow-md active:scale-95"
          >
            <Icon className="h-5 w-5 text-[hsl(25,90%,45%)]" />
            <span className="font-quicksand text-xs font-bold text-foreground">{action.label}</span>
          </button>
        );
      })}
    </div>
  );
}
