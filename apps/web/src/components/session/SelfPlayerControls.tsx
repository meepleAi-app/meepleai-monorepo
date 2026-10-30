'use client';

/**
 * I tre pezzi d'interfaccia del giocatore «io» nei wizard di creazione sessione (#4750): la
 * targhetta «Tu», «Io non gioco» per toglierlo, «Aggiungi me» per rimetterlo.
 *
 * Sono in un file solo perché i wizard sono più d'uno e le stesse parole devono dire la stessa
 * cosa in tutti.
 */

import { UserPlus } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export function SelfBadge() {
  return (
    <Badge variant="secondary" className="shrink-0">
      Tu
    </Badge>
  );
}

/** Un vero pulsante, con altezza di tocco di almeno 24 px (WCAG 2.5.8). */
export function OptOutButton({ onClick, className }: { onClick: () => void; className?: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'min-h-6 rounded text-xs text-muted-foreground underline underline-offset-2 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        className
      )}
    >
      Io non gioco
    </button>
  );
}

export function AddSelfButton({
  onClick,
  disabled,
  className,
}: {
  onClick: () => void;
  disabled?: boolean;
  className?: string;
}) {
  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={onClick}
      disabled={disabled}
      className={className}
    >
      <UserPlus className="h-4 w-4 mr-1" aria-hidden="true" />
      Aggiungi me
    </Button>
  );
}
