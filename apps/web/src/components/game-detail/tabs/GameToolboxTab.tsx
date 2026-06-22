'use client';

import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Toolbox tab — placeholder describing the toolbox feature.
 * The legacy /toolbox route now redirects to this tab, so a full-screen
 * link is intentionally NOT provided to avoid redirect loops.
 *
 * #2096 M4 (sub-issue #2188): refactor from 3-paragraph placeholder text
 * to 1-Card mockup-style structure (icon 44x44 + Title + Description +
 * body + CTA disabled). No data layer impl — toolkit listing deferred a
 * future EPIC. Icon uses entity-toolkit AA-compliant darker text variant
 * (text-entity-toolkit-text ~5.6:1 ✅ vs bg-entity-toolkit/12, per #1094
 * Real-C-E gamebook).
 */
export function GameToolboxTab({ variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per usare il toolbox.
        </p>
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
      <Card className="hover:translate-y-0 hover:shadow-sm dark:hover:shadow-sm">
        <CardHeader className="flex flex-row items-start gap-3 space-y-0">
          <div
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md bg-entity-toolkit/12 text-2xl text-entity-toolkit-text"
          >
            🧰
          </div>
          <div className="flex-1 space-y-1.5">
            <CardTitle className={variant === 'desktop' ? 'text-lg' : 'text-base'}>
              Toolbox
            </CardTitle>
            <CardDescription>
              Strumenti rapidi per il gioco: dadi, timer, punteggi, note e altro ancora.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent>
          <p className="text-xs italic text-muted-foreground">
            Integrazione completa del toolbox in arrivo.
          </p>
        </CardContent>
        <CardFooter>
          <Button variant="outline" disabled className="cursor-not-allowed">
            In arrivo
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
