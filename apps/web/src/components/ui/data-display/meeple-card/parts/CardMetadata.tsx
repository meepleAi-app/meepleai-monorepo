'use client';

/**
 * CardMetadata - Metadata chips rendering
 *
 * Wraps the existing MetadataChips component with variant-specific
 * visibility and layout logic.
 *
 * @module components/ui/data-display/meeple-card/parts/CardMetadata
 */

import React from 'react';

import { MetadataChips } from '../../meeple-card-parts';

import type { MeepleCardMetadata, MeepleCardVariant } from '../types';

export interface CardMetadataProps {
  /** Metadata items to display */
  metadata: MeepleCardMetadata[];
  /** Card layout variant */
  variant: MeepleCardVariant;
  /** Additional CSS class */
  className?: string;
}

/**
 * Renders metadata chips for non-grid and non-compact variants.
 * Grid variant uses a dedicated footer bar instead.
 * Compact variant hides metadata entirely.
 */
export function CardMetadata({ metadata, variant, className }: CardMetadataProps) {
  if (metadata.length === 0 || variant === 'compact' || variant === 'grid') {
    return null;
  }

  return (
    <MetadataChips
      metadata={metadata}
      variant={variant}
      className={variant === 'hero' ? 'mt-auto' : className}
    />
  );
}
