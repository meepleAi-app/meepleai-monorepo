import type { JSX, ReactNode } from 'react';

import type { EntityType } from '@/components/ui/entity-tokens';

export interface KPIStatCardProps {
  readonly label: string;
  readonly value: ReactNode;
  readonly sub?: string;
  readonly tone?: EntityType;
  readonly icon?: ReactNode;
  readonly className?: string;
}

const TONE_BORDER: Record<EntityType, string> = {
  game: 'border-t-entity-game',
  player: 'border-t-entity-player',
  session: 'border-t-entity-session',
  agent: 'border-t-entity-agent',
  kb: 'border-t-entity-document',
  chat: 'border-t-entity-chat',
  event: 'border-t-entity-event',
  toolkit: 'border-t-entity-toolkit',
  tool: 'border-t-entity-tool',
};

const TONE_VALUE_TEXT: Record<EntityType, string> = {
  game: 'text-entity-game',
  player: 'text-entity-player',
  session: 'text-entity-session',
  agent: 'text-entity-agent',
  kb: 'text-entity-document',
  chat: 'text-entity-chat',
  event: 'text-entity-event',
  toolkit: 'text-entity-toolkit',
  tool: 'text-entity-tool',
};

export function KPIStatCard({
  label,
  value,
  sub,
  tone = 'session',
  icon,
  className,
}: KPIStatCardProps): JSX.Element {
  return (
    <div
      className={[
        'flex min-w-0 flex-col gap-1',
        'rounded-lg border border-border border-t-[3px] bg-card',
        'px-4 py-3.5',
        TONE_BORDER[tone],
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <div className="flex items-center gap-1.5">
        {icon ? (
          <span aria-hidden="true" className="text-[13px] leading-none">
            {icon}
          </span>
        ) : null}
        <span className="font-mono text-[9.5px] font-bold uppercase tracking-wider text-muted-foreground">
          {label}
        </span>
      </div>
      <div
        className={[
          'font-display text-[28px] font-extrabold leading-none tabular-nums tracking-tight',
          TONE_VALUE_TEXT[tone],
        ].join(' ')}
      >
        {value}
      </div>
      {sub ? (
        <div className="font-mono text-[10px] font-bold text-muted-foreground">{sub}</div>
      ) : null}
    </div>
  );
}
