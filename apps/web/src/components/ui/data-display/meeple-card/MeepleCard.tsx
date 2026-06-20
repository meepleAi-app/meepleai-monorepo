'use client';

import { memo } from 'react';

import { MeepleCardAttributionFooter } from './MeepleCardAttributionFooter';
import { CompactCard } from './variants/CompactCard';
import { FeaturedCard } from './variants/FeaturedCard';
import { FocusCard } from './variants/FocusCard';
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
  focus: FocusCard,
} as const;

function MeepleCardImpl(props: MeepleCardProps) {
  const variant = props.variant ?? 'grid';
  const Renderer = variantMap[variant];

  // Issue #2055 Phase G AC-G6 — Wikidata cover attribution footer rendered
  // beneath any game card whose cover came from Wikidata. The footer
  // returns null internally when the license is missing, so this is
  // effectively a no-op for non-Wikidata covers.
  const showAttributionFooter = props.entity === 'game';

  return (
    <>
      <Renderer {...props} />
      {showAttributionFooter && (
        <MeepleCardAttributionFooter
          license={props.wikidataCoverLicense ?? null}
          attribution={props.wikidataCoverAttribution ?? null}
          sourceUrl={props.wikidataCoverSourceUrl ?? null}
        />
      )}
    </>
  );
}

export const MeepleCard = memo(MeepleCardImpl);
