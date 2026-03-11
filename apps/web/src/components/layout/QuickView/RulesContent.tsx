'use client';

import { BookOpen } from 'lucide-react';

interface RulesContentProps {
  gameId: string | null;
}

export function RulesContent({ gameId }: RulesContentProps) {
  return (
    <div data-testid="rules-content" className="flex flex-col h-full">
      {gameId === null ? (
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <BookOpen className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le regole</p>
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <BookOpen className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">
            Le regole verranno caricate dalla knowledge base del gioco
          </p>
        </div>
      )}
    </div>
  );
}
