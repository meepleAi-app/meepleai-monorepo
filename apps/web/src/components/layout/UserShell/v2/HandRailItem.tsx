'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';
import type { HandCard } from '@/stores/use-card-hand';

interface HandRailItemProps {
  card: HandCard;
  isActive: boolean;
}

const ENTITY_GRADIENTS: Record<string, string> = {
  game: 'linear-gradient(135deg, hsl(25 75% 78%), hsl(25 80% 55%))',
  session: 'linear-gradient(135deg, hsl(240 55% 75%), hsl(240 60% 55%))',
  chat: 'linear-gradient(135deg, hsl(220 72% 78%), hsl(220 80% 58%))',
  player: 'linear-gradient(135deg, hsl(262 78% 78%), hsl(262 83% 60%))',
  agent: 'linear-gradient(135deg, hsl(38 85% 75%), hsl(38 92% 55%))',
  kb: 'linear-gradient(135deg, hsl(210 55% 78%), hsl(210 40% 55%))',
  event: 'linear-gradient(135deg, hsl(350 75% 78%), hsl(350 89% 60%))',
  toolkit: 'linear-gradient(135deg, hsl(142 60% 78%), hsl(142 70% 45%))',
  tool: 'linear-gradient(135deg, hsl(195 70% 78%), hsl(195 80% 50%))',
};

const ENTITY_ACCENTS: Record<string, string> = {
  game: 'hsl(25 95% 45%)',
  session: 'hsl(240 60% 55%)',
  chat: 'hsl(220 80% 55%)',
  player: 'hsl(262 83% 58%)',
  agent: 'hsl(38 92% 50%)',
  kb: 'hsl(210 40% 55%)',
  event: 'hsl(350 89% 60%)',
  toolkit: 'hsl(142 70% 45%)',
  tool: 'hsl(195 80% 50%)',
};

const ENTITY_ICONS: Record<string, string> = {
  game: '🎲',
  session: '🎯',
  chat: '💬',
  player: '👤',
  agent: '🤖',
  kb: '📚',
  event: '📅',
  toolkit: '🧰',
  tool: '🔧',
};

export function HandRailItem({ card, isActive }: HandRailItemProps) {
  const gradient = ENTITY_GRADIENTS[card.entity] ?? ENTITY_GRADIENTS.game;
  const accent = ENTITY_ACCENTS[card.entity] ?? ENTITY_ACCENTS.game;
  const icon = ENTITY_ICONS[card.entity] ?? '🎲';

  return (
    <Link
      href={card.href}
      data-entity={card.entity}
      data-active={isActive}
      title={card.title}
      className={cn(
        'relative block w-[52px] h-[72px] rounded-[10px] bg-[var(--nh-bg-elevated)] border border-[var(--nh-border-default)] overflow-hidden shrink-0 transition-all duration-300 ease-out',
        'shadow-[var(--shadow-warm-sm)]',
        'hover:translate-x-[3px] hover:scale-105 hover:shadow-[var(--shadow-warm-md)]',
        isActive && 'translate-x-[6px] scale-110 shadow-[var(--shadow-warm-lg)]'
      )}
      style={isActive ? { outline: `2px solid ${accent}`, outlineOffset: '2px' } : undefined}
    >
      <span
        aria-hidden
        className="absolute left-0 top-0 bottom-0 w-[3px]"
        style={{ background: accent }}
      />
      <div
        className="w-full h-[42px] flex items-center justify-center text-lg"
        style={{ background: gradient }}
        aria-hidden
      >
        {card.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={card.imageUrl} alt="" className="w-full h-full object-cover" />
        ) : (
          icon
        )}
      </div>
      <span className="absolute bottom-[3px] left-[4px] right-[4px] text-[7px] font-bold font-[var(--font-quicksand)] leading-[1.1] text-[var(--nh-text-primary)] whitespace-nowrap overflow-hidden text-ellipsis text-center">
        {card.title}
      </span>
    </Link>
  );
}
