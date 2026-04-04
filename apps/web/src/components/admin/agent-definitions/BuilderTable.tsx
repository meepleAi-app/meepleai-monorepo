'use client';

import { ColumnDef } from '@tanstack/react-table';
import { ArchiveRestore, FlaskConical, MoreHorizontal, Pencil, Rocket, Trash2 } from 'lucide-react';
import Link from 'next/link';

import {
  Badge,
  Button,
  DataTable,
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui';
import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

interface BuilderTableProps {
  data: AgentDefinitionDto[];
  onDelete: (id: string) => void;
  onStartTesting?: (id: string) => void;
  onPublish?: (id: string) => void;
  onUnpublish?: (id: string) => void;
}

const STATUS_LABELS = ['Draft', 'Testing', 'Published'] as const;

function getStatusBadge(status: number) {
  const label = STATUS_LABELS[status] ?? 'Draft';
  switch (status) {
    case 1:
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {label}
        </Badge>
      );
    case 2:
      return (
        <Badge variant="default" className="bg-green-600">
          {label}
        </Badge>
      );
    default:
      return <Badge variant="secondary">{label}</Badge>;
  }
}

export function BuilderTable({
  data,
  onDelete,
  onStartTesting,
  onPublish,
  onUnpublish,
}: BuilderTableProps) {
  const columns: ColumnDef<AgentDefinitionDto>[] = [
    {
      accessorKey: 'name',
      header: 'Name',
    },
    {
      accessorKey: 'description',
      header: 'Description',
      cell: ({ row }) => (
        <span className="text-sm text-muted-foreground line-clamp-1">
          {row.original.description || 'No description'}
        </span>
      ),
    },
    {
      accessorKey: 'config.model',
      header: 'Model',
      cell: ({ row }) => <Badge variant="secondary">{row.original.config.model}</Badge>,
    },
    {
      id: 'status',
      header: 'Status',
      cell: ({ row }) => {
        const agent = row.original;
        return (
          <div className="flex items-center gap-2">
            {getStatusBadge(agent.status ?? 0)}
            <Badge variant={agent.isActive ? 'default' : 'outline'}>
              {agent.isActive ? 'Active' : 'Inactive'}
            </Badge>
          </div>
        );
      },
    },
    {
      accessorKey: 'createdAt',
      header: 'Created',
      cell: ({ row }) => new Date(row.original.createdAt).toLocaleDateString(),
    },
    {
      id: 'actions',
      cell: ({ row }) => {
        const agent = row.original;
        const status = agent.status ?? 0;

        return (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon">
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem asChild>
                <Link href={`/admin/agents/definitions/${agent.id}/edit`}>
                  <Pencil className="h-4 w-4 mr-2" />
                  Edit
                </Link>
              </DropdownMenuItem>

              {/* Lifecycle actions */}
              {status === 0 && onStartTesting && (
                <DropdownMenuItem onClick={() => onStartTesting(agent.id)}>
                  <FlaskConical className="h-4 w-4 mr-2" />
                  Start Testing
                </DropdownMenuItem>
              )}
              {status === 1 && onPublish && (
                <DropdownMenuItem onClick={() => onPublish(agent.id)}>
                  <Rocket className="h-4 w-4 mr-2" />
                  Publish
                </DropdownMenuItem>
              )}
              {status === 2 && onUnpublish && (
                <DropdownMenuItem onClick={() => onUnpublish(agent.id)}>
                  <ArchiveRestore className="h-4 w-4 mr-2" />
                  Unpublish
                </DropdownMenuItem>
              )}

              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => onDelete(agent.id)} className="text-destructive">
                <Trash2 className="h-4 w-4 mr-2" />
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        );
      },
    },
  ];

  return <DataTable columns={columns} data={data} />;
}
