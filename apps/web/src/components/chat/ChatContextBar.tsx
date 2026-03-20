'use client';

import { Bot, MessageSquare } from 'lucide-react';

interface ChatContextBarProps {
  agentName?: string;
  gameContext?: string;
}

export function ChatContextBar({ agentName = 'MeepleBot', gameContext }: ChatContextBarProps) {
  return (
    <div className="flex items-center gap-3 w-full text-sm">
      <div className="flex items-center gap-1.5 shrink-0">
        <Bot className="w-4 h-4 text-amber-500" />
        <span className="font-medium text-foreground">{agentName}</span>
      </div>
      {gameContext && (
        <span className="flex items-center gap-1 text-muted-foreground shrink-0">
          <MessageSquare className="w-3.5 h-3.5" />
          Contesto: {gameContext}
        </span>
      )}
      {!gameContext && <span className="text-muted-foreground">Generale</span>}
    </div>
  );
}
