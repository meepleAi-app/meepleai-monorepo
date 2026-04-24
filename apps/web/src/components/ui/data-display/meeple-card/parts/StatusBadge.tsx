import { statusColors } from '../tokens';

import type { CardStatus } from '../types';

interface StatusBadgeProps {
  status: CardStatus;
  className?: string;
  /**
   * When true, renders without absolute positioning (no top/left).
   * Used when the badge is wrapped in an external flex stack container
   * (e.g. GridCard's BadgeStack). Default: false (legacy absolute positioning).
   */
  stacked?: boolean;
}

const statusLabels: Partial<Record<CardStatus, string>> = {
  owned: 'Posseduto',
};

export function StatusBadge({ status, className = '', stacked = false }: StatusBadgeProps) {
  const colors = statusColors[status];
  if (!colors) return null;

  const label = statusLabels[status] ?? status;

  const positioning = stacked ? 'self-start' : 'absolute left-2.5 top-7 z-10';

  return (
    <span
      className={`${positioning} rounded-[5px] px-[7px] py-[1px] text-[9px] font-bold ${className}`}
      style={{ background: colors.bg, color: colors.text }}
    >
      {label}
    </span>
  );
}
