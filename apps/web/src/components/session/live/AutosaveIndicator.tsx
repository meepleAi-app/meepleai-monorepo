'use client';

/**
 * AutosaveIndicator
 *
 * Minimal status badge shown on the live scores page to communicate
 * autosave activity to the user. States map to the server-side Quartz
 * autosave job lifecycle.
 *
 * Issue #325
 */

import { CheckCircle2, Loader2, AlertCircle } from 'lucide-react';

import { cn } from '@/lib/utils';

export type AutosaveState = 'idle' | 'saving' | 'saved' | 'error';

interface AutosaveIndicatorProps {
  state?: AutosaveState;
  className?: string;
}

const STATE_CONFIG: Record<
  AutosaveState,
  { label: string; icon: React.ElementType; className: string }
> = {
  idle: {
    label: 'Pronto',
    icon: CheckCircle2,
    className: 'text-muted-foreground',
  },
  saving: {
    label: 'Salvataggio…',
    icon: Loader2,
    className: 'text-blue-500 animate-spin',
  },
  saved: {
    label: 'Salvato',
    icon: CheckCircle2,
    className: 'text-green-600',
  },
  error: {
    label: 'Errore salvataggio',
    icon: AlertCircle,
    className: 'text-destructive',
  },
};

export function AutosaveIndicator({ state = 'idle', className }: AutosaveIndicatorProps) {
  const config = STATE_CONFIG[state];
  const Icon = config.icon;

  return (
    <div
      data-testid="autosave-indicator"
      aria-label={config.label}
      aria-live="polite"
      className={cn('flex items-center gap-1.5 text-xs font-nunito', className)}
    >
      <Icon className={cn('h-3.5 w-3.5', config.className)} />
      <span className={config.className}>{config.label}</span>
    </div>
  );
}
