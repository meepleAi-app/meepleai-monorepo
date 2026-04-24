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
 *
 * Step 2 (2026-04-24): renamed from buildKbNavItems to buildKbConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
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

/**
 * @deprecated Use `buildKbConnections` instead. Will be removed in commit 8 of
 * the Step 2 migration PR.
 */
export const buildKbNavItems = buildKbConnections;

/** @deprecated Use `KbConnectionsCounts` instead. */
export type KbNavCounts = KbConnectionsCounts;

/** @deprecated Use `KbConnectionsHandlers` instead. */
export type KbNavHandlers = KbConnectionsHandlers;
