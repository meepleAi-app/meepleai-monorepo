'use client';

import { useState } from 'react';

import { Bot, Camera, Dice5, Send } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useSessionStore } from '@/store/session';

interface ActivityFeedInputBarProps {
  playerId: string;
  playerName: string;
  onDiceClick?: () => void;
  onCameraClick?: () => void;
  onAIClick?: () => void;
}

export function ActivityFeedInputBar({
  playerId,
  playerName,
  onDiceClick,
  onCameraClick,
  onAIClick,
}: ActivityFeedInputBarProps) {
  const [text, setText] = useState('');
  const addEvent = useSessionStore(s => s.addEvent);

  const handleSend = () => {
    if (!text.trim()) return;
    addEvent({
      id: crypto.randomUUID(),
      type: 'note',
      playerId,
      data: { playerName, text: text.trim() },
      timestamp: new Date().toISOString(),
    });
    setText('');
  };

  return (
    <div className="flex items-center gap-2 px-3 py-2 border-t border-border bg-card">
      <input
        value={text}
        onChange={e => setText(e.target.value)}
        onKeyDown={e => e.key === 'Enter' && handleSend()}
        placeholder="Scrivi una nota..."
        className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
      />
      <div className="flex items-center gap-1 shrink-0">
        <button
          onClick={onDiceClick}
          aria-label="Dadi"
          className="p-1.5 rounded-md text-muted-foreground hover:text-foreground transition-colors"
        >
          <Dice5 className="h-4 w-4" />
        </button>
        <button
          onClick={onCameraClick}
          aria-label="Foto"
          className="p-1.5 rounded-md text-muted-foreground hover:text-foreground transition-colors"
        >
          <Camera className="h-4 w-4" />
        </button>
        <button
          onClick={onAIClick}
          aria-label="AI"
          className="p-1.5 rounded-md text-muted-foreground hover:text-foreground transition-colors"
        >
          <Bot className="h-4 w-4" />
        </button>
        <button
          onClick={handleSend}
          aria-label="Invia nota"
          className={cn(
            'p-1.5 rounded-md transition-colors',
            text.trim() ? 'text-primary hover:bg-primary/10' : 'text-muted-foreground'
          )}
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
