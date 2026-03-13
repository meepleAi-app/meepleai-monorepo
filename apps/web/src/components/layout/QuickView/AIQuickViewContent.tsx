'use client';

import { useState } from 'react';

import { Bot, Send } from 'lucide-react';

import { cn } from '@/lib/utils';

const QUICK_PROMPTS = [
  'Spiega le regole base',
  'Suggerisci una strategia',
  'Regole per principianti',
  'Domande frequenti',
];

interface AIQuickViewContentProps {
  gameId: string;
  gameName: string;
}

export function AIQuickViewContent({ gameId: _gameId, gameName }: AIQuickViewContentProps) {
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<Array<{ role: 'user' | 'ai'; text: string }>>([]);

  const handleSend = (text: string) => {
    if (!text.trim()) return;
    setMessages(prev => [...prev, { role: 'user', text }]);
    setInput('');
  };

  return (
    <div className="flex flex-col h-full">
      {/* Context label */}
      <div className="flex items-center gap-2 px-3 py-2 border-b border-border">
        <Bot className="h-4 w-4 text-purple-500" />
        <span className="text-xs font-medium text-muted-foreground">
          AI assistente — {gameName}
        </span>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-3 space-y-3">
        {messages.length === 0 && (
          <div className="text-center py-6">
            <Bot className="h-8 w-8 mx-auto text-muted-foreground/50 mb-2" />
            <p className="text-sm text-muted-foreground">Chiedi qualcosa su {gameName}</p>
          </div>
        )}
        {messages.map((msg, i) => (
          <div
            key={i}
            className={cn(
              'text-sm rounded-lg px-3 py-2',
              msg.role === 'user' ? 'bg-primary/10 ml-6' : 'bg-muted mr-6'
            )}
          >
            {msg.text}
          </div>
        ))}
      </div>

      {/* Quick prompts */}
      <div className="flex flex-wrap gap-1.5 px-3 py-2 border-t border-border">
        {QUICK_PROMPTS.map(prompt => (
          <button
            key={prompt}
            onClick={() => handleSend(prompt)}
            className="text-xs px-2 py-1 rounded-full bg-muted text-muted-foreground hover:bg-muted/80 transition-colors"
          >
            {prompt}
          </button>
        ))}
      </div>

      {/* Input */}
      <div className="flex items-center gap-2 px-3 py-2 border-t border-border">
        <input
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleSend(input)}
          placeholder="Chiedi all'AI..."
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
        />
        <button
          onClick={() => handleSend(input)}
          aria-label="Invia messaggio"
          className="p-1.5 rounded-md text-primary hover:bg-primary/10 transition-colors"
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
