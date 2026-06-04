'use client';

/**
 * MetricSelector (#1840 SP5 F4-C7) — input combo con autocomplete dinamico per
 * la lista metric Prometheus, usato dal CreateAlertRuleDialog.
 *
 * Strategia UI: usa HTML5 `<datalist>` come autocomplete nativo per evitare
 * scope creep di un Combobox completo. Quando Prometheus è offline, il backend
 * serve un fallback list hardcoded e il componente surfaca un'inline warning
 * (`isFallback === true`).
 *
 * Token semantici only — compliant con `local/no-hardcoded-color-utility`.
 */

import { useId } from 'react';

import { AlertTriangle } from 'lucide-react';

import { usePrometheusMetricLabels } from '@/hooks/usePrometheusMetricLabels';
import { cn } from '@/lib/utils';

export interface MetricSelectorProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  required?: boolean;
  className?: string;
  id?: string;
  placeholder?: string;
}

export function MetricSelector({
  value,
  onChange,
  disabled,
  required,
  className,
  id,
  placeholder = 'es. meepleai_chat_p95_ms',
}: MetricSelectorProps) {
  const reactId = useId();
  const listId = id ? `${id}-options` : `metric-selector-${reactId}-options`;
  const { data, isLoading, isError } = usePrometheusMetricLabels();
  const labels = data?.labels ?? [];
  const isFallback = data?.isFallback ?? false;

  return (
    <div className={cn('space-y-1', className)}>
      <input
        type="text"
        id={id}
        list={listId}
        value={value}
        onChange={e => onChange(e.target.value)}
        disabled={disabled || isLoading}
        required={required}
        placeholder={isLoading ? 'Carico metriche…' : placeholder}
        className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring disabled:opacity-60"
        aria-describedby={isFallback || isError ? `${listId}-warning` : undefined}
        autoComplete="off"
        spellCheck={false}
      />
      <datalist id={listId}>
        {labels.map(label => (
          <option key={label} value={label} />
        ))}
      </datalist>

      {(isFallback || isError) && (
        <p
          id={`${listId}-warning`}
          role="status"
          className="flex items-start gap-1.5 font-mono text-[11px] text-amber-700 dark:text-amber-300"
        >
          <AlertTriangle aria-hidden className="mt-0.5 h-3 w-3 shrink-0" strokeWidth={2.5} />
          {isError
            ? 'Errore caricamento metriche · puoi comunque inserire il nome manualmente.'
            : 'Prometheus offline · fallback list cached. Le metriche potrebbero non essere aggiornate.'}
        </p>
      )}
    </div>
  );
}
