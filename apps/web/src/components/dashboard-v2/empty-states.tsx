/**
 * EmptyState Component - Issue #4581
 * Empty state messages for dashboard sections
 */

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

type EmptyStateVariant = 'no-games' | 'no-sessions' | 'no-upcoming';

interface EmptyStateProps {
  variant: EmptyStateVariant;
  onAction?: () => void;
}

const emptyStateConfig = {
  'no-games': {
    icon: '📚',
    title: 'Nessun gioco nella collezione',
    description: 'Aggiungi i tuoi primi giochi per iniziare a tracciare le partite!',
    actionLabel: '+ Aggiungi Gioco',
  },
  'no-sessions': {
    icon: '🎲',
    title: 'Nessuna partita recente',
    description: 'Registra la tua prima sessione di gioco!',
    actionLabel: '+ Nuova Partita',
  },
  'no-upcoming': {
    icon: '📅',
    title: 'Nessuna partita programmata',
    description: 'Pianifica la tua prossima sessione di gioco!',
    actionLabel: '+ Pianifica',
  },
};

export function EmptyState({ variant, onAction }: EmptyStateProps) {
  const config = emptyStateConfig[variant];

  return (
    <Card className="bg-white/70 backdrop-blur-md border-2 border-dashed border-muted">
      <CardContent className="flex flex-col items-center justify-center py-12 text-center">
        <div className="text-6xl mb-4" role="img" aria-label={config.title}>
          {config.icon}
        </div>
        <h3 className="font-quicksand font-semibold text-lg mb-2">{config.title}</h3>
        <p className="text-sm text-muted-foreground mb-6 max-w-md font-nunito">
          {config.description}
        </p>
        {onAction && (
          <Button onClick={onAction} variant="outline" className="min-h-[44px]">
            {config.actionLabel}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
