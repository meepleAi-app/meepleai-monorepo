/**
 * EntityTableView - Table view mode for EntityListView
 *
 * Renders entity data in a sortable table with entity-colored row accents,
 * row selection, and navigation footer support.
 *
 * Uses existing DataTable component from data-display with auto-generated
 * or custom column definitions.
 *
 * @module components/ui/data-display/entity-list-view/components/entity-table-view
 * @see Issue #4697
 */
/* eslint-disable @typescript-eslint/no-non-null-assertion -- pre-existing pattern: array/object access guarded by length/key check or by upstream validator; assertion is correct by construction. Cleanup tracked for follow-up audit. */

'use client';

import React, { useMemo, useState } from 'react';

import { Star } from 'lucide-react';

import { cn } from '@/lib/utils';

import { DataTable, SortableHeader } from '../../data-table';

import type { MeepleCardProps, MeepleEntityType } from '../../meeple-card';
import type { TableColumnConfig } from '../entity-list-view.types';
import type { ColumnDef, SortingState } from '@tanstack/react-table';

// ============================================================================
// Entity color maps (canonical tokens — Issue #2955 Fase 2)
//
// Literal static maps (ESLint-friendly, no template literals / getEntityToken).
// - Row accent border → `border-entity-<e>` base token (`--c-<e>`); borders
//   only need 3:1, so the base — not the darker `-text` — is correct.
// - Badge pill → soft tint `bg-entity-<e>/10` + AA label `text-entity-<e>-text`.
// kb resolves to the registered TEAL `-kb` / `-kb-text`, NEVER `-document`.
// gameNightEvent reuses the canonical `event` token (#1929 WP2).
// ============================================================================

const ENTITY_BORDER: Record<MeepleEntityType, string> = {
  game: 'border-entity-game',
  player: 'border-entity-player',
  session: 'border-entity-session',
  agent: 'border-entity-agent',
  kb: 'border-entity-kb',
  chat: 'border-entity-chat',
  event: 'border-entity-event',
  toolkit: 'border-entity-toolkit',
  tool: 'border-entity-tool',
  gameNightEvent: 'border-entity-event',
};

const ENTITY_BADGE_BG: Record<MeepleEntityType, string> = {
  game: 'bg-entity-game/10',
  player: 'bg-entity-player/10',
  session: 'bg-entity-session/10',
  agent: 'bg-entity-agent/10',
  kb: 'bg-entity-kb/10',
  chat: 'bg-entity-chat/10',
  event: 'bg-entity-event/10',
  toolkit: 'bg-entity-toolkit/10',
  tool: 'bg-entity-tool/10',
  gameNightEvent: 'bg-entity-event/10',
};

const ENTITY_TEXT: Record<MeepleEntityType, string> = {
  game: 'text-entity-game-text',
  player: 'text-entity-player-text',
  session: 'text-entity-session-text',
  agent: 'text-entity-agent-text',
  kb: 'text-entity-kb-text',
  chat: 'text-entity-chat-text',
  event: 'text-entity-event-text',
  toolkit: 'text-entity-toolkit-text',
  tool: 'text-entity-tool-text',
  gameNightEvent: 'text-entity-event-text',
};

// ============================================================================
// Types
// ============================================================================

/** Row data structure that wraps renderItem output with original item reference */
interface TableRowData {
  _originalIndex: number;
  id: string;
  title: string;
  subtitle?: string;
  rating?: number;
  ratingMax?: number;
  badge?: string;
  imageUrl?: string;
  [key: string]: unknown;
}

export interface EntityTableViewProps<T> {
  /** Items already through search/filter/sort pipeline */
  displayItems: T[];
  /** Original items array for click handler mapping */
  items: T[];
  /** Entity type for color accents */
  entity: MeepleEntityType;
  /** Transform function */
  renderItem: (item: T) => Omit<MeepleCardProps, 'entity' | 'variant'>;
  /** Custom table column definitions */
  tableColumns?: TableColumnConfig[];
  /** Row click handler */
  onItemClick?: (item: T) => void;
  /** Empty state message */
  emptyMessage?: string;
  /** Test ID */
  'data-testid'?: string;
}

// ============================================================================
// Auto-generate columns from MeepleCardProps
// ============================================================================

function createDefaultColumns(entity: MeepleEntityType): ColumnDef<TableRowData, unknown>[] {
  const badgeClass = cn(ENTITY_BADGE_BG[entity], ENTITY_TEXT[entity]);
  return [
    {
      accessorKey: 'title',
      header: ({ column }) => <SortableHeader column={column}>Title</SortableHeader>,
      cell: ({ row }) => (
        <div className="flex items-center gap-3">
          {row.original.imageUrl && (
            <img
              src={row.original.imageUrl}
              alt=""
              className="w-8 h-8 rounded object-cover flex-shrink-0"
            />
          )}
          <div>
            <span className="font-medium font-quicksand">{row.original.title}</span>
            {row.original.badge && (
              <span
                className={cn(
                  'ml-2 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold uppercase',
                  badgeClass
                )}
              >
                {row.original.badge}
              </span>
            )}
          </div>
        </div>
      ),
    },
    {
      accessorKey: 'subtitle',
      header: ({ column }) => <SortableHeader column={column}>Details</SortableHeader>,
      cell: ({ row }) => (
        <span className="text-muted-foreground font-nunito text-sm">
          {row.original.subtitle || '—'}
        </span>
      ),
    },
    {
      accessorKey: 'rating',
      header: ({ column }) => <SortableHeader column={column}>Rating</SortableHeader>,
      cell: ({ row }) => {
        const rating = row.original.rating;
        if (rating == null) return <span className="text-muted-foreground">—</span>;
        const max = row.original.ratingMax ?? 10;
        return (
          <div className="flex items-center gap-1.5">
            <Star className="w-3.5 h-3.5 fill-amber-400 text-amber-400" />
            <span className="font-nunito text-sm font-medium">
              {rating.toFixed(1)}/{max}
            </span>
          </div>
        );
      },
    },
  ];
}

function createColumnsFromConfig(configs: TableColumnConfig[]): ColumnDef<TableRowData, unknown>[] {
  return configs.map(config => ({
    id: config.id,
    accessorKey: config.accessorKey || config.id,
    header:
      config.sortable !== false
        ? ({
            column,
          }: {
            column: {
              getIsSorted: () => false | 'asc' | 'desc';
              toggleSorting: (desc?: boolean) => void;
            };
          }) => <SortableHeader column={column}>{config.header}</SortableHeader>
        : () => <span>{config.header}</span>,
    cell: config.cell
      ? ({ row }: { row: { original: TableRowData } }) => {
          const key = config.accessorKey || config.id;
          return config.cell!(row.original[key], row.original);
        }
      : ({ row }: { row: { original: TableRowData } }) => {
          const key = config.accessorKey || config.id;
          const val = row.original[key];
          return <span className="font-nunito text-sm">{val != null ? String(val) : '—'}</span>;
        },
    size: config.width ? undefined : undefined,
  }));
}

// ============================================================================
// Main Component
// ============================================================================

export function EntityTableView<T>({
  displayItems,
  items: _items,
  entity,
  renderItem,
  tableColumns: customColumns,
  onItemClick,
  emptyMessage,
  'data-testid': testId,
}: EntityTableViewProps<T>) {
  const [sorting, setSorting] = useState<SortingState>([]);

  // Transform items to table row data
  const tableData = useMemo<TableRowData[]>(() => {
    return displayItems.map((item, idx) => {
      const cardProps = renderItem(item);
      return {
        _originalIndex: idx,
        id: cardProps.id || `item-${idx}`,
        title: cardProps.title,
        subtitle: cardProps.subtitle,
        rating: cardProps.rating,
        ratingMax: cardProps.ratingMax,
        badge: cardProps.badge,
        imageUrl: cardProps.imageUrl,
        // Spread any metadata values for custom columns
        ...(cardProps.metadata?.reduce(
          (acc, m, i) => {
            acc[`meta_${i}`] = m.value;
            return acc;
          },
          {} as Record<string, unknown>
        ) ?? {}),
      };
    });
  }, [displayItems, renderItem]);

  // Build columns
  const columns = useMemo<ColumnDef<TableRowData, unknown>[]>(() => {
    if (customColumns && customColumns.length > 0) {
      return createColumnsFromConfig(customColumns);
    }
    return createDefaultColumns(entity);
  }, [customColumns, entity]);

  // Row click handler
  const handleRowClick = useMemo(() => {
    if (!onItemClick) return undefined;
    return (row: TableRowData) => {
      const originalItem = displayItems[row._originalIndex];
      if (originalItem) onItemClick(originalItem);
    };
  }, [onItemClick, displayItems]);

  // Entity-colored row styling via canonical token class (#2955 Fase 2)
  const borderClass = ENTITY_BORDER[entity] || '';

  return (
    <div
      data-testid={testId || 'table-layout'}
      className={cn('[&_tbody_tr]:border-l-4', borderClass, 'font-nunito')}
    >
      <DataTable
        columns={columns}
        data={tableData}
        sorting={sorting}
        onSortingChange={setSorting}
        onRowClick={handleRowClick}
        emptyMessage={emptyMessage}
        getRowId={row => row.id}
      />
    </div>
  );
}
