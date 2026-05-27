import type React from 'react';

import { AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

interface Props {
  readonly label: string;
  readonly message?: string;
  readonly onRetry?: () => void;
}

export function SectionErrorCard({ label, message, onRetry }: Props): React.JSX.Element {
  return (
    <div
      data-testid="section-error-card"
      role="alert"
      className="bg-destructive/5 border border-destructive/30 rounded-lg p-6 flex items-start gap-4"
    >
      <AlertCircle className="h-5 w-5 text-destructive shrink-0 mt-0.5" aria-hidden />
      <div className="flex-1">
        <h3 className="font-quicksand font-bold text-foreground">Errore caricamento — {label}</h3>
        <p className="text-sm text-muted-foreground mt-1">
          {message ?? 'Impossibile caricare questa sezione. Riprova.'}
        </p>
        {onRetry && (
          <Button size="sm" variant="outline" className="mt-3" onClick={onRetry}>
            Riprova
          </Button>
        )}
      </div>
    </div>
  );
}
