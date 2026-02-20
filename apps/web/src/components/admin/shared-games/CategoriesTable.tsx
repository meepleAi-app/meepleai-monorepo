/**
 * CategoriesTable Component
 * Issue #4862: Admin table with EntityTableView design system
 *
 * Displays game categories using EntityTableView entity="game" with orange accents,
 * sortable columns for name, game count, and description.
 */

'use client';

import React from 'react';

import { EntityTableView } from '@/components/ui/data-display/entity-list-view';
import type { TableColumnConfig } from '@/components/ui/data-display/entity-list-view/entity-list-view.types';

export interface GameCategory {
  id: string;
  name: string;
  gameCount: number;
  description?: string;
}

interface CategoriesTableProps {
  categories: GameCategory[];
  onCategoryClick?: (category: GameCategory) => void;
  className?: string;
}

const tableColumns: TableColumnConfig[] = [
  {
    id: 'title',
    header: 'Name',
    accessorKey: 'title',
  },
  {
    id: 'gameCount',
    header: 'Games',
    accessorKey: 'meta_0',
    cell: (value) => (
      <span className="font-mono text-sm">{String(value ?? '0')}</span>
    ),
  },
  {
    id: 'description',
    header: 'Description',
    accessorKey: 'subtitle',
    sortable: false,
    cell: (value) => (
      <span className="text-sm text-muted-foreground line-clamp-1">
        {value ? String(value) : '—'}
      </span>
    ),
  },
];

export function CategoriesTable({
  categories,
  onCategoryClick,
  className,
}: CategoriesTableProps) {
  return (
    <div className={className}>
      <EntityTableView
        displayItems={categories}
        items={categories}
        entity="game"
        renderItem={(category) => ({
          title: category.name,
          id: category.id,
          subtitle: category.description,
          metadata: [{ value: String(category.gameCount) }],
        })}
        tableColumns={tableColumns}
        onItemClick={onCategoryClick}
        emptyMessage="No categories found"
        data-testid="categories-table"
      />
    </div>
  );
}
