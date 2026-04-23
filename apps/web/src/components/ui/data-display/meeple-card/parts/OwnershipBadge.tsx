'use client';

import { Archive, CheckCircle2, Heart, type LucideIcon } from 'lucide-react';

import type { OwnershipBadge as OwnershipValue } from '../types';

interface OwnershipConfig {
  icon: LucideIcon;
  fg: string;
  bg: string;
  fill?: boolean;
}

const CONFIG: Record<OwnershipValue, OwnershipConfig> = {
  owned: {
    icon: CheckCircle2,
    fg: 'hsl(152 76% 40%)',
    bg: 'hsl(152 76% 40% / 0.12)',
    fill: true,
  },
  wishlist: {
    icon: Heart,
    fg: 'hsl(350 89% 60%)',
    bg: 'hsl(350 89% 60% / 0.12)',
    fill: true,
  },
  archived: {
    icon: Archive,
    fg: 'hsl(215 20% 50%)',
    bg: 'hsl(215 20% 50% / 0.08)',
  },
};

const LABEL: Record<OwnershipValue, string> = {
  owned: 'Owned',
  wishlist: 'Wishlist',
  archived: 'Archived',
};

interface OwnershipBadgeProps {
  value: OwnershipValue;
  size?: number;
  className?: string;
}

export function OwnershipBadge({ value, size = 20, className }: OwnershipBadgeProps) {
  const cfg = CONFIG[value];
  const Icon = cfg.icon;

  return (
    <span
      data-testid={`ownership-badge-${value}`}
      aria-label={LABEL[value]}
      title={LABEL[value]}
      className={`inline-flex items-center justify-center rounded-full ${className ?? ''}`.trim()}
      style={{
        width: size,
        height: size,
        background: cfg.bg,
        color: cfg.fg,
      }}
    >
      <Icon
        size={Math.round(size * 0.65)}
        strokeWidth={1.75}
        fill={cfg.fill ? 'currentColor' : 'none'}
        aria-hidden="true"
      />
    </span>
  );
}
