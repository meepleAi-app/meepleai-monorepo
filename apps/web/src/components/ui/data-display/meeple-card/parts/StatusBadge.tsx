import { statusColors } from '../tokens';

import type { CardStatus } from '../types';

interface StatusBadgeProps {
  status: CardStatus;
  className?: string;
}

const statusLabels: Partial<Record<CardStatus, string>> = {
  owned: 'Posseduto',
};

export function StatusBadge({ status, className = '' }: StatusBadgeProps) {
  const colors = statusColors[status];
  if (!colors) return null;

  const label = statusLabels[status] ?? status;

  return (
    <span
      className={`absolute left-2.5 top-7 z-10 rounded-[5px] px-[7px] py-[1px] text-[9px] font-bold ${className}`}
      style={{ background: colors.bg, color: colors.text }}
    >
      {label}
    </span>
  );
}
