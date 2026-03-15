'use client';

import Link from 'next/link';

import type { HandCard } from '@/stores/use-card-hand';

import { ENTITY_COLORS, ENTITY_EMOJIS } from './entity-hand-constants';

interface HandDrawerCardProps {
  card: HandCard;
  isFocused: boolean;
  onClick: () => void;
}

export function HandDrawerCard({ card, isFocused, onClick }: HandDrawerCardProps) {
  const entityColor = ENTITY_COLORS[card.entity] ?? ENTITY_COLORS.custom;
  const entityEmoji = ENTITY_EMOJIS[card.entity] ?? ENTITY_EMOJIS.custom;

  const focusedStyle: React.CSSProperties = isFocused
    ? {
        opacity: 1,
        border: `1.5px solid hsl(${entityColor} / 0.6)`,
        boxShadow: `0 0 8px hsl(${entityColor} / 0.2)`,
      }
    : {
        opacity: 0.45,
        border: '1.5px solid transparent',
      };

  return (
    <Link
      href={card.href}
      role="link"
      onClick={onClick}
      aria-current={isFocused ? 'page' : undefined}
      className="block rounded-lg transition-all duration-200 ease-in-out hover:opacity-70 hover:-translate-y-0.5"
      style={{
        width: 36,
        height: 36,
        overflow: 'hidden',
        ...focusedStyle,
      }}
    >
      {card.imageUrl ? (
        <img
          src={card.imageUrl}
          alt={card.title}
          className="h-full w-full object-cover rounded-lg"
          style={{ display: 'block' }}
        />
      ) : (
        <span
          aria-label={card.title}
          className="flex h-full w-full items-center justify-center rounded-lg text-sm"
          style={{
            background: `linear-gradient(135deg, hsl(${entityColor} / 0.25), hsl(${entityColor} / 0.45))`,
          }}
        >
          {entityEmoji}
        </span>
      )}
    </Link>
  );
}
