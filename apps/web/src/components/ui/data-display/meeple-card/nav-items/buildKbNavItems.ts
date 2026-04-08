import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface KbNavCounts {
  chunkCount?: number;
}

export interface KbNavHandlers {
  onChunksClick?: () => void;
  onReindexClick?: () => void;
  onPreviewClick?: () => void;
  onDownloadClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for KB document entity cards.
 *
 * Slots: Chunks | Reindex (action) | Anteprima (action) | Download (action)
 */
export function buildKbNavItems(counts: KbNavCounts, handlers: KbNavHandlers): NavFooterItem[] {
  return [
    {
      icon: navIcons.chunks,
      label: 'Chunks',
      entity: 'kb',
      count:
        counts.chunkCount !== undefined && counts.chunkCount > 0 ? counts.chunkCount : undefined,
      disabled: counts.chunkCount === undefined || !handlers.onChunksClick,
      onClick: handlers.onChunksClick,
    },
    {
      icon: navIcons.reindex,
      label: 'Reindex',
      entity: 'kb',
      disabled: !handlers.onReindexClick,
      onClick: handlers.onReindexClick,
    },
    {
      icon: navIcons.preview,
      label: 'Anteprima',
      entity: 'kb',
      disabled: !handlers.onPreviewClick,
      onClick: handlers.onPreviewClick,
    },
    {
      icon: navIcons.download,
      label: 'Download',
      entity: 'kb',
      disabled: !handlers.onDownloadClick,
      onClick: handlers.onDownloadClick,
    },
  ];
}
