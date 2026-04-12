'use client';

import Link from 'next/link';

import { entityHsl, entityIcon } from '@/components/ui/data-display/meeple-card/tokens';
import type { HandCard } from '@/lib/stores/card-hand-store';
import { cn } from '@/lib/utils';

interface HandRailCardProps {
  card: HandCard;
  expanded: boolean;
  active: boolean;
}

export function HandRailCard({ card, expanded, active }: HandRailCardProps) {
  const color = entityHsl(card.entityType);
  const icon = entityIcon[card.entityType];

  return (
    <Link
      href={card.href}
      title={card.label}
      aria-label={card.label}
      aria-current={active ? 'page' : undefined}
      data-testid={`rail-card-${card.id}`}
      className={cn(
        'flex items-center gap-2 w-full px-1 py-1 rounded-lg border',
        'transition-all duration-150 overflow-hidden min-h-[36px]',
        active
          ? 'bg-white/8 border-white/14'
          : 'border-transparent hover:bg-white/7 hover:border-white/10'
      )}
    >
      <span
        className="flex-shrink-0 w-[26px] h-[18px] rounded flex items-center justify-center text-[9px] font-bold"
        style={{
          background: entityHsl(card.entityType, 0.14),
          borderLeft: `2.5px solid ${color}`,
        }}
        aria-hidden="true"
      >
        {icon}
      </span>

      <span
        className={cn(
          'flex flex-col overflow-hidden transition-[opacity,max-width] duration-150',
          expanded ? 'opacity-100 max-w-[140px]' : 'opacity-0 max-w-0'
        )}
      >
        <span className="text-[9.5px] font-bold text-white/80 truncate leading-tight">
          {card.label}
        </span>
        {card.sublabel && (
          <span className="text-[7.5px] text-white/38 truncate">{card.sublabel}</span>
        )}
      </span>

      {expanded && card.pinned && (
        <span className="ml-auto text-[7px] text-white/30 flex-shrink-0" aria-hidden="true">
          📌
        </span>
      )}
    </Link>
  );
}
