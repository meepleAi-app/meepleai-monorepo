'use client';

/**
 * Agent Typology Table Columns (Issue #3179)
 *
 * TanStack Table column definitions for agent typology management.
 * Includes sortable columns, status badges, and action menu.
 */

import { type ColumnDef } from '@tanstack/react-table';
import { MoreHorizontal, Edit, CheckCircle, Trash2 } from 'lucide-react';

import { SortableHeader } from '@/components/ui/data-display/data-table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import type { AgentTypology } from '@/lib/api/clients/adminClient';

import { TypologyStatusBadge } from './TypologyStatusBadge';

export interface TypologyTableActions {
  onEdit?: (typology: AgentTypology) => void;
  onApprove?: (typology: AgentTypology) => void;
  onDelete?: (typology: AgentTypology) => void;
}

function formatDate(dateString: string): string {
  const date = new Date(dateString);
  if (isNaN(date.getTime())) return 'Invalid date';
  return date.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function createTypologyColumns(
  actions?: TypologyTableActions
): ColumnDef<AgentTypology>[] {
  return [
    // Name column (sortable)
    {
      accessorKey: 'name',
      header: ({ column }) => <SortableHeader column={column}>Name</SortableHeader>,
      cell: ({ row }) => {
        const typology = row.original;
        return <div className="font-medium">{typology.name}</div>;
      },
    },

    // Description column
    {
      accessorKey: 'description',
      header: 'Description',
      cell: ({ row }) => {
        const typology = row.original;
        const maxLength = 80;
        const truncated =
          typology.description.length > maxLength
            ? `${typology.description.slice(0, maxLength)}...`
            : typology.description;
        return (
          <div className="text-sm text-muted-foreground max-w-md" title={typology.description}>
            {truncated}
          </div>
        );
      },
      enableSorting: false,
    },

    // Status column
    {
      accessorKey: 'status',
      header: 'Status',
      cell: ({ row }) => {
        const typology = row.original;
        return <TypologyStatusBadge status={typology.status} />;
      },
      enableSorting: false,
    },

    // Created By column
    {
      accessorKey: 'createdByDisplayName',
      header: 'Created By',
      cell: ({ row }) => {
        const typology = row.original;
        return <div className="text-sm">{typology.createdByDisplayName}</div>;
      },
      enableSorting: false,
    },

    // Created At column (sortable)
    {
      accessorKey: 'createdAt',
      header: ({ column }) => <SortableHeader column={column}>Created At</SortableHeader>,
      cell: ({ row }) => {
        const typology = row.original;
        return <div className="text-sm text-muted-foreground">{formatDate(typology.createdAt)}</div>;
      },
    },

    // Actions column
    {
      id: 'actions',
      header: 'Actions',
      cell: ({ row }) => {
        const typology = row.original;
        const isPending = typology.status === 'PendingReview';

        return (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                <span className="sr-only">Open menu</span>
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>Actions</DropdownMenuLabel>
              <DropdownMenuSeparator />

              {/* Edit (all statuses) */}
              <DropdownMenuItem
                onClick={e => {
                  e.stopPropagation();
                  actions?.onEdit?.(typology);
                }}
              >
                <Edit className="h-4 w-4 mr-2" />
                Edit
              </DropdownMenuItem>

              {/* Approve (PendingReview only) */}
              {isPending && (
                <DropdownMenuItem
                  onClick={e => {
                    e.stopPropagation();
                    actions?.onApprove?.(typology);
                  }}
                >
                  <CheckCircle className="h-4 w-4 mr-2" />
                  Approve
                </DropdownMenuItem>
              )}

              <DropdownMenuSeparator />

              {/* Delete (destructive) */}
              <DropdownMenuItem
                onClick={e => {
                  e.stopPropagation();
                  actions?.onDelete?.(typology);
                }}
                className="text-destructive"
              >
                <Trash2 className="h-4 w-4 mr-2" />
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        );
      },
      enableSorting: false,
      enableHiding: false,
    },
  ];
}
