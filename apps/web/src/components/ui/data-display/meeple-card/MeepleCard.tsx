'use client';

import { memo } from 'react';

import { CompactCard } from './variants/CompactCard';
import { FeaturedCard } from './variants/FeaturedCard';
import { GridCard } from './variants/GridCard';
import { HeroCard } from './variants/HeroCard';
import { ListCard } from './variants/ListCard';

import type { MeepleCardProps } from './types';

const variantMap = {
  grid: GridCard,
  list: ListCard,
  compact: CompactCard,
  featured: FeaturedCard,
  hero: HeroCard,
} as const;

export const MeepleCard = memo(function MeepleCard(props: MeepleCardProps) {
  const variant = props.variant ?? 'grid';
  const Renderer = variantMap[variant];
  return <Renderer {...props} />;
});
