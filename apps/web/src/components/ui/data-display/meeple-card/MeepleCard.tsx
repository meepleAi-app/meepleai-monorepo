'use client';

import { memo } from 'react';
import type { MeepleCardProps } from './types';
import { GridCard } from './variants/GridCard';
import { ListCard } from './variants/ListCard';
import { CompactCard } from './variants/CompactCard';
import { FeaturedCard } from './variants/FeaturedCard';
import { HeroCard } from './variants/HeroCard';

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
