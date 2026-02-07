'use client';

import { type ColumnDef } from '@tanstack/react-table';
import { MoreHorizontal, Eye, Edit, Ban, UserCheck, Trash2, UserCog, Shield } from 'lucide-react';
import Link from 'next/link';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/data-display/avatar';
import {
  createSelectColumn,
  SortableHeader,
} from '@/components/ui/data-display/data-table';
import { UserRoleBadge, type UserRole } from '@/components/ui/data-display/user-role-badge';
import {
  UserStatusIndicator,
  getUserStatus,
  type UserStatus,
} from '@/components/ui/data-display/user-status-indicator';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';

export type User = {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  role: UserRole;
  tier?: string;              // Issue #3698: User tier (Free, Basic, Pro, Enterprise)
  tokenUsage?: number;        // Issue #3698: Tokens used this month
  tokenLimit?: number;        // Issue #3698: Monthly token limit
  createdAt: string;
  lastSeenAt: string | null;
  isSuspended?: boolean;
  suspendReason?: string | null;
  libraryCount?: number;
};

export interface UsersTableActions {
  onEdit?: (user: User) => void;
  onSuspend?: (user: User) => void;
  onUnsuspend?: (user: User) => void;
  onDelete?: (user: User) => void;
  onImpersonate?: (user: User) => void;
  onChangeTier?: (user: User) => void; // Issue #3699
}

function getInitials(name: string): string {
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

function formatDate(dateString: string | null): string {
  if (!dateString) return 'Never';
  const date = new Date(dateString);
  if (isNaN(date.getTime())) return 'Never';
  return date.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatRelativeDate(dateString: string | null): string {
  if (!dateString) return 'Never';
  const date = new Date(dateString);
  // Validate that the date is valid
  if (isNaN(date.getTime())) return 'Never';
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
  if (diffDays < 365) return `${Math.floor(diffDays / 30)} months ago`;
  return formatDate(dateString);
}

export function createUsersColumns(actions?: UsersTableActions): ColumnDef<User>[] {
  return [
    // Checkbox column
    createSelectColumn<User>(),

    // Avatar column
    {
      id: 'avatar',
      header: '',
      cell: ({ row }) => {
        const user = row.original;
        return (
          <Avatar className="h-8 w-8">
            {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt={user.displayName} />}
            <AvatarFallback className="text-xs">{getInitials(user.displayName)}</AvatarFallback>
          </Avatar>
        );
      },
      enableSorting: false,
      enableHiding: false,
    },

    // Name column
    {
      accessorKey: 'displayName',
      header: ({ column }) => <SortableHeader column={column}>Name</SortableHeader>,
      cell: ({ row }) => {
        const user = row.original;
        return (
          <div className="font-medium">
            <Link
              href={`/admin/users/${user.id}`}
              className="hover:underline"
              onClick={e => e.stopPropagation()}
            >
              {user.displayName}
            </Link>
          </div>
        );
      },
    },

    // Email column
    {
      accessorKey: 'email',
      header: ({ column }) => <SortableHeader column={column}>Email</SortableHeader>,
      cell: ({ row }) => (
        <div className="text-muted-foreground">{row.getValue('email')}</div>
      ),
    },

    // Role column
    {
      accessorKey: 'role',
      header: ({ column }) => <SortableHeader column={column}>Role</SortableHeader>,
      cell: ({ row }) => <UserRoleBadge role={row.getValue('role')} />,
      filterFn: (row, id, value: string[]) => value.includes(row.getValue(id)),
    },

    // Tier column - Issue #3698
    {
      accessorKey: 'tier',
      header: ({ column }) => <SortableHeader column={column}>Tier</SortableHeader>,
      cell: ({ row }) => {
        const tier = row.getValue('tier') as string | undefined;
        const tierColors = {
          free: 'bg-gray-100 text-gray-800',
          basic: 'bg-blue-100 text-blue-800',
          pro: 'bg-purple-100 text-purple-800',
          enterprise: 'bg-orange-100 text-orange-800',
        };
        const colorClass = tierColors[tier?.toLowerCase() as keyof typeof tierColors] || 'bg-gray-100 text-gray-800';
        return (
          <div className={`inline-flex items-center px-2 py-1 rounded text-xs font-medium ${colorClass}`}>
            {tier || 'Free'}
          </div>
        );
      },
    },

    // Token Usage column - Issue #3698
    {
      accessorKey: 'tokenUsage',
      header: ({ column }) => <SortableHeader column={column}>Token Usage</SortableHeader>,
      cell: ({ row }) => {
        const usage = row.getValue('tokenUsage') as number | undefined;
        const limit = row.original.tokenLimit || 10_000;
        const percentage = usage && limit ? Math.round((usage / limit) * 100) : 0;

        const colorClass = percentage >= 100 ? 'text-red-600' : percentage >= 80 ? 'text-orange-600' : 'text-green-600';

        return (
          <div className="text-sm">
            <div className={`font-medium ${colorClass}`}>
              {usage?.toLocaleString() || '0'} / {limit.toLocaleString()}
            </div>
            <div className="text-xs text-muted-foreground">
              {percentage}%
            </div>
          </div>
        );
      },
    },

    // Status column
    {
      id: 'status',
      header: 'Status',
      cell: ({ row }) => {
        const user = row.original;
        const status: UserStatus = getUserStatus(user);
        return <UserStatusIndicator status={status} />;
      },
      filterFn: (row, _id, value: string[]) => {
        const status = getUserStatus(row.original);
        return value.includes(status);
      },
    },

    // Last Active column
    {
      accessorKey: 'lastSeenAt',
      header: ({ column }) => <SortableHeader column={column}>Last Active</SortableHeader>,
      cell: ({ row }) => (
        <div className="text-muted-foreground text-sm">
          {formatRelativeDate(row.getValue('lastSeenAt'))}
        </div>
      ),
    },

    // Library column
    {
      accessorKey: 'libraryCount',
      header: ({ column }) => <SortableHeader column={column}>Library</SortableHeader>,
      cell: ({ row }) => {
        const count = row.getValue('libraryCount') as number | undefined;
        return (
          <div className="text-muted-foreground text-sm">
            {count !== undefined ? `${count} games` : '-'}
          </div>
        );
      },
    },

    // Actions column
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => {
        const user = row.original;

        return (
          <DropdownMenu>
            <DropdownMenuTrigger asChild onClick={e => e.stopPropagation()}>
              <Button variant="ghost" className="h-8 w-8 p-0">
                <span className="sr-only">Open menu</span>
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>Actions</DropdownMenuLabel>
              <DropdownMenuItem asChild>
                <Link href={`/admin/users/${user.id}`}>
                  <Eye className="mr-2 h-4 w-4" />
                  View Details
                </Link>
              </DropdownMenuItem>
              {actions?.onEdit && (
                <DropdownMenuItem onClick={() => actions.onEdit?.(user)}>
                  <Edit className="mr-2 h-4 w-4" />
                  Edit
                </DropdownMenuItem>
              )}
              {actions?.onChangeTier && (
                <DropdownMenuItem onClick={() => actions.onChangeTier?.(user)}>
                  <Shield className="mr-2 h-4 w-4" />
                  Change Tier
                </DropdownMenuItem>
              )}
              {actions?.onImpersonate && user.role !== 'Admin' && !user.isSuspended && (
                <DropdownMenuItem
                  onClick={() => actions.onImpersonate?.(user)}
                  className="text-blue-600 focus:text-blue-600"
                >
                  <UserCog className="mr-2 h-4 w-4" />
                  Impersonate
                </DropdownMenuItem>
              )}
              <DropdownMenuSeparator />
              {user.isSuspended ? (
                actions?.onUnsuspend && (
                  <DropdownMenuItem onClick={() => actions.onUnsuspend?.(user)}>
                    <UserCheck className="mr-2 h-4 w-4" />
                    Activate
                  </DropdownMenuItem>
                )
              ) : (
                actions?.onSuspend && (
                  <DropdownMenuItem
                    onClick={() => actions.onSuspend?.(user)}
                    className="text-orange-600 focus:text-orange-600"
                  >
                    <Ban className="mr-2 h-4 w-4" />
                    Suspend
                  </DropdownMenuItem>
                )
              )}
              {actions?.onDelete && (
                <DropdownMenuItem
                  onClick={() => actions.onDelete?.(user)}
                  className="text-destructive focus:text-destructive"
                >
                  <Trash2 className="mr-2 h-4 w-4" />
                  Delete
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        );
      },
      enableSorting: false,
      enableHiding: false,
    },
  ];
}
