import type { ConnectionChipProps } from '../types';

export interface KbConnectionsCounts {
  chunkCount?: number;
}

export interface KbConnectionsHandlers {
  onChunksClick?: () => void;
  onReindexClick?: () => void;
  onPreviewClick?: () => void;
  onDownloadClick?: () => void;
}

/**
 * Build the 4-slot connection channel for KB document entity cards.
 *
 * Slots: Chunks | Reindex (action) | Anteprima (action) | Download (action)
 */
export function buildKbConnections(
  counts: KbConnectionsCounts,
  handlers: KbConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Chunks',
      entityType: 'kb',
      count:
        counts.chunkCount !== undefined && counts.chunkCount > 0 ? counts.chunkCount : undefined,
      disabled: counts.chunkCount === undefined || !handlers.onChunksClick,
      onClick: handlers.onChunksClick,
    },
    {
      label: 'Reindex',
      entityType: 'kb',
      disabled: !handlers.onReindexClick,
      onClick: handlers.onReindexClick,
    },
    {
      label: 'Anteprima',
      entityType: 'kb',
      disabled: !handlers.onPreviewClick,
      onClick: handlers.onPreviewClick,
    },
    {
      label: 'Download',
      entityType: 'kb',
      disabled: !handlers.onDownloadClick,
      onClick: handlers.onDownloadClick,
    },
  ];
}
