'use client';

import { AlertTriangle, CheckCheck, Loader2, Settings2, type LucideIcon } from 'lucide-react';

import { entityHsl } from '../tokens';

import type { LifecycleState, MeepleEntityType } from '../types';

interface LifecycleStateBadgeProps {
  value: LifecycleState;
  entityType: MeepleEntityType;
  className?: string;
}

interface StaticConfig {
  kind: 'icon';
  icon: LucideIcon;
  color: string;
  label: string;
  spin?: boolean;
}

interface DotConfig {
  kind: 'dot';
  fillEntity: boolean;
  outline?: boolean;
  label: string;
  pulse?: boolean;
}

const CONFIG: Record<LifecycleState, StaticConfig | DotConfig> = {
  active: { kind: 'dot', fillEntity: true, label: 'Active', pulse: true },
  idle: { kind: 'dot', fillEntity: false, outline: true, label: 'Idle' },
  completed: {
    kind: 'icon',
    icon: CheckCheck,
    color: 'hsl(152 76% 40%)',
    label: 'Completed',
  },
  setup: {
    kind: 'icon',
    icon: Settings2,
    color: 'hsl(38 92% 50%)',
    label: 'Setup',
  },
  processing: {
    kind: 'icon',
    icon: Loader2,
    color: 'hsl(200 89% 55%)',
    label: 'Processing',
    spin: true,
  },
  failed: {
    kind: 'icon',
    icon: AlertTriangle,
    color: 'hsl(0 84% 60%)',
    label: 'Failed',
  },
};

export function LifecycleStateBadge({ value, entityType, className }: LifecycleStateBadgeProps) {
  const cfg = CONFIG[value];

  if (cfg.kind === 'dot') {
    const color = cfg.fillEntity ? entityHsl(entityType) : 'hsl(215 20% 60%)';
    return (
      <span
        data-testid={`lifecycle-badge-${value}`}
        aria-label={cfg.label}
        title={cfg.label}
        className={`inline-block rounded-full ${cfg.pulse ? 'animate-pulse' : ''} ${className ?? ''}`.trim()}
        style={{
          width: 8,
          height: 8,
          background: cfg.outline ? 'transparent' : color,
          border: cfg.outline ? `1.5px solid ${color}` : 'none',
        }}
      />
    );
  }

  const Icon = cfg.icon;
  return (
    <span
      data-testid={`lifecycle-badge-${value}`}
      aria-label={cfg.label}
      title={cfg.label}
      className={`inline-flex items-center ${className ?? ''}`.trim()}
      style={{ color: cfg.color }}
    >
      <Icon
        size={14}
        strokeWidth={1.75}
        aria-hidden="true"
        className={cfg.spin ? 'animate-spin' : undefined}
      />
    </span>
  );
}
