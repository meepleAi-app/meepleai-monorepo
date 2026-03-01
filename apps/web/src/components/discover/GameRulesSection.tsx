'use client';

import { BookOpen, ExternalLink } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { GameRules } from '@/lib/api/schemas/shared-games.schemas';

interface GameRulesSectionProps {
  rules: GameRules | null;
  bggId: number | null;
  gameTitle: string;
}

export function GameRulesSection({ rules, bggId, gameTitle }: GameRulesSectionProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <BookOpen className="h-5 w-5" />
          Regole del Gioco
        </CardTitle>
      </CardHeader>
      <CardContent>
        {rules?.content ? (
          <div className="max-h-[500px] overflow-y-auto pr-1">
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-foreground">
              {rules.content}
            </p>
            {rules.language && (
              <p className="mt-4 text-xs text-muted-foreground">
                Lingua: {rules.language.toUpperCase()}
              </p>
            )}
          </div>
        ) : (
          <div className="flex flex-col items-center gap-3 py-8 text-center">
            <BookOpen className="h-10 w-10 text-muted-foreground/40" />
            <div>
              <p className="text-sm font-medium text-foreground">
                Regole non ancora disponibili per {gameTitle}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                Le regole non sono ancora state caricate nel catalogo.
              </p>
            </div>
            {bggId && (
              <a
                href={`https://boardgamegeek.com/boardgame/${bggId}/files`}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 rounded-md border border-input bg-background px-3 py-1.5 text-sm font-medium shadow-sm hover:bg-accent hover:text-accent-foreground transition-colors"
              >
                <ExternalLink className="h-3.5 w-3.5" />
                Cerca su BoardGameGeek
              </a>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
