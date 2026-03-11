'use client';

import { HelpCircle } from 'lucide-react';

interface FaqContentProps {
  gameId: string | null;
}

export function FaqContent({ gameId }: FaqContentProps) {
  return (
    <div data-testid="faq-content" className="flex flex-col h-full">
      {gameId === null ? (
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <HelpCircle className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le FAQ</p>
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <HelpCircle className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">
            Le FAQ verranno caricate dalla knowledge base del gioco
          </p>
        </div>
      )}
    </div>
  );
}
