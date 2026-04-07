/**
 * useMetadataToMeepleCard — maps GameMetadata + CoverImageSelection → MeepleCardProps.
 * Debounces updates by 300ms to avoid flicker during rapid typing.
 */

import { useEffect, useState } from 'react';



import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { CoverImageSelection, GameMetadata } from '@/stores/useGameImportWizardStore';

const DEBOUNCE_MS = 300;

function buildProps(metadata: GameMetadata | null, cover: CoverImageSelection): MeepleCardProps {
  const imageUrl = cover.mode !== 'placeholder' ? (cover.imageUrl ?? undefined) : undefined;

  const metaItems: MeepleCardProps['metadata'] = [];

  if (metadata?.minPlayers != null || metadata?.maxPlayers != null) {
    const players =
      metadata.minPlayers != null && metadata.maxPlayers != null
        ? `${metadata.minPlayers}–${metadata.maxPlayers}`
        : `${metadata?.minPlayers ?? metadata?.maxPlayers}`;
    metaItems.push({ label: `${players} giocatori` });
  }

  if (metadata?.playingTimeMinutes != null) {
    metaItems.push({ label: `${metadata.playingTimeMinutes} min` });
  }

  if (metadata?.yearPublished != null) {
    metaItems.push({ label: String(metadata.yearPublished) });
  }

  return {
    entity: 'game',
    variant: 'featured',
    title: metadata?.title || 'Titolo gioco',
    subtitle: metadata?.publishers?.join(', ') || metadata?.designers?.join(', ') || undefined,
    imageUrl,
    metadata: metaItems.length > 0 ? metaItems : undefined,
  };
}

export function useMetadataToMeepleCard(
  metadata: GameMetadata | null,
  cover: CoverImageSelection
): MeepleCardProps {
  const [props, setProps] = useState<MeepleCardProps>(() => buildProps(metadata, cover));

  useEffect(() => {
    const timer = setTimeout(() => {
      setProps(buildProps(metadata, cover));
    }, DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [metadata, cover]);

  return props;
}
