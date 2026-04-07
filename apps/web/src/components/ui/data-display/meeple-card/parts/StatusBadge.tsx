import { statusColors } from '../tokens';
import type { CardStatus } from '../types';

interface StatusBadgeProps {
  status: CardStatus;
  className?: string;
}

export function StatusBadge({ status, className = '' }: StatusBadgeProps) {
  const colors = statusColors[status];
  if (!colors) return null;

  return (
    <span
      className={`absolute left-2.5 top-7 z-10 rounded-[5px] px-[7px] py-[1px] text-[9px] font-bold ${className}`}
      style={{ background: colors.bg, color: colors.text }}
    >
      {status}
    </span>
  );
}
