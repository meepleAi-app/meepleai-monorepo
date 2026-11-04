'use client';

import { memo } from 'react';

import { MeepleCardAttributionFooter } from './MeepleCardAttributionFooter';
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

/**
 * Issue #4120 — testid predefinito sulla radice della scheda.
 *
 * Il contratto espone `'data-testid'` da sempre e tutte le varianti lo propagano alla propria
 * radice, ma **nessun chiamante lo passava**: nel DOM non c'era alcun hook stabile per le schede.
 * Il risultato era che le spec E2E cercavano `game-card` (106 sedi, strascico della migrazione
 * GameCard → MeepleCard) e `meeple-card` (30 sedi), e nessuno dei due esisteva.
 *
 * Il default è additivo: un `data-testid` esplicito continua a vincere, e `data-entity={entity}`
 * — già emesso da ogni variante — resta il discriminante, quindi una scheda di gioco si seleziona
 * con `[data-testid="meeple-card"][data-entity="game"]`. Una sola radice per scheda: i due siti di
 * GridCard sono rami mutuamente esclusivi (href → Link, altrimenti div), quindi la modalità strict
 * di Playwright non trova duplicati.
 */
const DEFAULT_TEST_ID = 'meeple-card';

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
      <Renderer {...props} data-testid={props['data-testid'] ?? DEFAULT_TEST_ID} />
      {showAttributionFooter && (
        <MeepleCardAttributionFooter
          license={props.coverLicense ?? null}
          attribution={props.coverAttribution ?? null}
          sourceUrl={props.coverSourceUrl ?? null}
        />
      )}
    </>
  );
}

export const MeepleCard = memo(MeepleCardImpl);
