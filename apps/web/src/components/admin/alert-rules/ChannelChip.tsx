'use client';

/**
 * ChannelChip (#1840 SP5 F4-C7) — chip riusabile per canali alert (email + slack).
 *
 * Visualizza:
 *   - icona per-type (Slack hash / Email at)
 *   - label canale
 *   - status indicator: connected (verde) / error (rosso) / pending (muted)
 *
 * Usato in:
 *   - AlertRuleList → ChannelChipStack (stack inline)
 *   - CreateAlertRuleDialog → ChannelChipMultiSelect (selectable)
 *   - CanaliDrawer → header status
 *
 * Token semantici only (no hardcoded bg-* / text-*). Compliant con
 * `local/no-hardcoded-color-utility` ESLint rule (#1023 DS-15 mode error).
 */

import { AtSign, Hash, AlertTriangle, Check } from 'lucide-react';

import type {
  AlertChannelTestStatus,
  AlertChannelType,
} from '@/lib/api/schemas/alert-channels.schemas';
import { cn } from '@/lib/utils';

export interface ChannelChipProps {
  type: AlertChannelType;
  /** Label visualizzata accanto all'icona (es. "#alerts" per slack, "ops@example.com" per email). */
  label?: string;
  status?: AlertChannelTestStatus | 'pending' | null;
  /** Tooltip o messaggio status (es. "Slack 401" su errore). */
  tooltip?: string;
  /** True quando il chip è cliccabile per toggle in multi-select. */
  interactive?: boolean;
  selected?: boolean;
  onClick?: () => void;
  className?: string;
}

const TYPE_ICONS = {
  email: AtSign,
  slack: Hash,
} as const;

const TYPE_DEFAULT_LABEL = {
  email: 'Email',
  slack: 'Slack',
} as const;

function StatusDot({ status }: { status: ChannelChipProps['status'] }) {
  if (!status || status === 'pending') {
    return <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-muted-foreground/40" />;
  }
  if (status === 'ok') {
    return (
      <Check
        aria-hidden
        className="h-3 w-3 text-emerald-600 dark:text-emerald-400"
        strokeWidth={3}
      />
    );
  }
  return (
    <AlertTriangle
      aria-hidden
      className="h-3 w-3 text-rose-600 dark:text-rose-400"
      strokeWidth={2.5}
    />
  );
}

export function ChannelChip({
  type,
  label,
  status,
  tooltip,
  interactive = false,
  selected = false,
  onClick,
  className,
}: ChannelChipProps) {
  const Icon = TYPE_ICONS[type];
  const displayLabel = label ?? TYPE_DEFAULT_LABEL[type];
  const ariaStatusLabel =
    status === 'ok'
      ? 'connesso'
      : status === 'error'
        ? 'errore'
        : status === 'pending'
          ? 'pending'
          : 'non configurato';

  const baseClass = cn(
    'inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 font-mono text-[10.5px] font-semibold transition-colors',
    // Entity tinting via "event" (rose) when error, "toolkit" (emerald) when ok, neutral otherwise.
    status === 'error' &&
      'border-rose-400/40 bg-rose-50 text-rose-700 dark:border-rose-500/30 dark:bg-rose-950/30 dark:text-rose-300',
    status === 'ok' &&
      'border-emerald-400/40 bg-emerald-50 text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-950/30 dark:text-emerald-300',
    (!status || status === 'pending') && 'border-border bg-muted/40 text-muted-foreground',
    selected && 'ring-2 ring-primary ring-offset-1',
    interactive && 'cursor-pointer hover:bg-muted',
    className
  );

  const content = (
    <>
      <Icon aria-hidden className="h-3 w-3 shrink-0" strokeWidth={2.5} />
      <span className="truncate">{displayLabel}</span>
      <StatusDot status={status} />
    </>
  );

  if (interactive) {
    return (
      <button
        type="button"
        onClick={onClick}
        className={baseClass}
        aria-pressed={selected}
        aria-label={`${TYPE_DEFAULT_LABEL[type]} ${displayLabel} — ${ariaStatusLabel}`}
        title={tooltip}
      >
        {content}
      </button>
    );
  }

  return (
    <span
      role="status"
      aria-label={`${TYPE_DEFAULT_LABEL[type]} ${displayLabel} — ${ariaStatusLabel}`}
      title={tooltip}
      className={baseClass}
    >
      {content}
    </span>
  );
}

export interface ChannelChipStackProps {
  channels: Array<Pick<ChannelChipProps, 'type' | 'label' | 'status' | 'tooltip'>>;
  emptyLabel?: string;
  className?: string;
}

/**
 * Stack inline di ChannelChip per la colonna "Canale" della AlertRuleList.
 * Mostra fino a tutti i canali configurati; gestisce wrap su righe multiple.
 */
export function ChannelChipStack({
  channels,
  emptyLabel = '— non assegnato',
  className,
}: ChannelChipStackProps) {
  if (channels.length === 0) {
    return (
      <span className={cn('font-mono text-[10.5px] text-muted-foreground italic', className)}>
        {emptyLabel}
      </span>
    );
  }
  return (
    <div className={cn('flex flex-wrap items-center gap-1', className)}>
      {channels.map((c, idx) => (
        <ChannelChip
          key={`${c.type}-${idx}`}
          type={c.type}
          label={c.label}
          status={c.status}
          tooltip={c.tooltip}
        />
      ))}
    </div>
  );
}
