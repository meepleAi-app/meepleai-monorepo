'use client';

import * as React from 'react';

import { useRouter } from 'next/navigation';

import {
  DataTable,
  type SortingState,
  type RowSelectionState,
} from '@/components/ui/data-display/data-table';

import { createUsersColumns, type User, type UsersTableActions } from './users-columns';

export interface UsersDataTableProps {
  users: User[];
  isLoading?: boolean;
  sorting?: SortingState;
  onSortingChange?: (sorting: SortingState) => void;
  rowSelection?: RowSelectionState;
  onRowSelectionChange?: (selection: RowSelectionState) => void;
  actions?: UsersTableActions;
}

export function UsersDataTable({
  users,
  isLoading = false,
  sorting,
  onSortingChange,
  rowSelection,
  onRowSelectionChange,
  actions,
}: UsersDataTableProps) {
  const router = useRouter();

  const columns = React.useMemo(() => createUsersColumns(actions), [actions]);

  const handleRowClick = React.useCallback(
    (user: User) => {
      router.push(`/admin/users/${user.id}`);
    },
    [router]
  );

  return (
    <DataTable
      columns={columns}
      data={users}
      sorting={sorting}
      onSortingChange={updater => {
        if (typeof updater === 'function') {
          onSortingChange?.(updater(sorting ?? []));
        } else {
          onSortingChange?.(updater);
        }
      }}
      rowSelection={rowSelection}
      onRowSelectionChange={updater => {
        if (typeof updater === 'function') {
          onRowSelectionChange?.(updater(rowSelection ?? {}));
        } else {
          onRowSelectionChange?.(updater);
        }
      }}
      onRowClick={handleRowClick}
      getRowId={user => user.id}
      isLoading={isLoading}
      emptyMessage="No users found."
      enableShiftSelection={true}
    />
  );
}

// Re-export types for convenience
export type { User, UsersTableActions } from './users-columns';
