'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { Trash2, Download, Shield, Ban } from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard, BulkActionBar } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import type { SortingState, RowSelectionState } from '@/components/ui/data-display/data-table';
import { type UserRole } from '@/components/ui/data-display/user-role-badge';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import { UsersDataTable, type User as UsersTableUser, type UsersTableActions } from './_components';

// Types - use User from users-columns
type User = UsersTableUser;

type CreateUserRequest = {
  email: string;
  password: string;
  displayName: string;
  role: string;
};

type UpdateUserRequest = {
  email?: string;
  displayName?: string;
  role?: string;
};

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type ConfirmationDialog = {
  isOpen: boolean;
  title: string;
  message: string;
  onConfirm: () => void;
};

type ModalState = {
  isOpen: boolean;
  mode: 'create' | 'edit';
  user?: User;
};

type BulkRoleModalState = {
  isOpen: boolean;
  newRole: string;
};

export function AdminPageClient() {
  const { user, loading: authLoading } = useAuthUser();

  if (!user) return null;
  // State
  const [users, setUsers] = useState<User[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [sorting, setSorting] = useState<SortingState>([{ id: 'lastSeenAt', desc: true }]);
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [confirmation, setConfirmation] = useState<ConfirmationDialog>({
    isOpen: false,
    title: '',
    message: '',
    onConfirm: () => {},
  });
  const [modal, setModal] = useState<ModalState>({
    isOpen: false,
    mode: 'create',
  });
  const [selectedUsers, setSelectedUsers] = useState<Set<string>>(new Set());
  const [bulkRoleModal, setBulkRoleModal] = useState<BulkRoleModalState>({
    isOpen: false,
    newRole: 'User',
  });
  const [isBulkProcessing, setIsBulkProcessing] = useState(false);

  // Row selection conversion helpers
  const rowSelection = useMemo<RowSelectionState>(() =>
    Object.fromEntries(Array.from(selectedUsers).map(id => [id, true])),
    [selectedUsers]
  );

  const handleRowSelectionChange = useCallback((updater: RowSelectionState | ((old: RowSelectionState) => RowSelectionState)) => {
    const newSelection = typeof updater === 'function' ? updater(rowSelection) : updater;
    setSelectedUsers(new Set(Object.keys(newSelection).filter(id => newSelection[id])));
  }, [rowSelection]);

  // Toast management
  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts(prev => prev.filter(t => t.id !== id));
  }, []);

  // Fetch users
  const fetchUsers = useCallback(async () => {
    try {
      setDataLoading(true);
      setError(null);

      // Extract sort from TanStack sorting state
      const sortBy = sorting[0]?.id || 'lastSeenAt';
      const sortOrder = sorting[0]?.desc ? 'desc' : 'asc';

      const queryParams = new URLSearchParams({
        page: page.toString(),
        limit: pageSize.toString(),
        sortBy,
        sortOrder,
      });

      if (search) {
        queryParams.append('search', search);
      }
      if (roleFilter !== 'all') {
        queryParams.append('role', roleFilter);
      }

      const result = await api.admin.getUsers({
        page,
        pageSize: 20,
        search: search || undefined,
        role: roleFilter !== 'all' ? roleFilter : undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });

      // eslint-disable-next-line @typescript-eslint/no-explicit-any -- API response type coercion
      setUsers(result.items as any);
      setTotal(result.total);
      setDataLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
      setDataLoading(false);
      addToast('error', 'Failed to load users');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- addToast is stable (defined with useCallback and empty deps)
  }, [page, pageSize, search, roleFilter, statusFilter, sorting]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  // Create user
  const handleCreate = useCallback(
    async (userData: CreateUserRequest) => {
      try {
        await api.admin.createUser(userData);
        addToast('success', `User ${userData.email} created successfully`);
        setModal({ isOpen: false, mode: 'create' });
        fetchUsers();
      } catch (err) {
        addToast('error', err instanceof Error ? err.message : 'Failed to create user');
      }
    },
    [addToast, fetchUsers]
  );

  // Update user
  const handleUpdate = useCallback(
    async (userId: string, updates: UpdateUserRequest) => {
      try {
        await api.admin.updateUser(userId, updates);
        addToast('success', 'User updated successfully');
        setModal({ isOpen: false, mode: 'create' });
        fetchUsers();
      } catch (err) {
        addToast('error', err instanceof Error ? err.message : 'Failed to update user');
      }
    },
    [addToast, fetchUsers]
  );

  // Delete user
  const handleDelete = useCallback(
    async (userId: string, email: string) => {
      setConfirmation({
        isOpen: true,
        title: 'Delete User',
        message: `Are you sure you want to delete ${email}? This action cannot be undone.`,
        onConfirm: async () => {
          try {
            await api.admin.deleteUser(userId);
            addToast('success', `User ${email} deleted successfully`);
            setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
            fetchUsers();
          } catch (err) {
            addToast('error', err instanceof Error ? err.message : 'Failed to delete user');
            setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          }
        },
      });
    },
    [addToast, fetchUsers]
  );

  // Suspend user
  const handleSuspend = useCallback(
    async (userId: string, email: string) => {
      setConfirmation({
        isOpen: true,
        title: 'Suspend User',
        message: `Are you sure you want to suspend ${email}? They will not be able to login.`,
        onConfirm: async () => {
          try {
            await api.admin.suspendUser(userId, 'Suspended by admin');
            addToast('success', `User ${email} suspended successfully`);
            setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
            fetchUsers();
          } catch (err) {
            addToast('error', err instanceof Error ? err.message : 'Failed to suspend user');
            setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          }
        },
      });
    },
    [addToast, fetchUsers]
  );

  // Unsuspend user
  const handleUnsuspend = useCallback(
    async (userId: string, email: string) => {
      try {
        await api.admin.unsuspendUser(userId);
        addToast('success', `User ${email} reactivated successfully`);
        fetchUsers();
      } catch (err) {
        addToast('error', err instanceof Error ? err.message : 'Failed to unsuspend user');
      }
    },
    [addToast, fetchUsers]
  );

  // Bulk delete
  const handleBulkDelete = useCallback(() => {
    if (selectedUsers.size === 0) return;

    setConfirmation({
      isOpen: true,
      title: 'Delete Multiple Users',
      message: `Are you sure you want to delete ${selectedUsers.size} user(s)? This action cannot be undone.`,
      onConfirm: async () => {
        try {
          await Promise.all(Array.from(selectedUsers).map(id => api.admin.deleteUser(id)));
          addToast('success', `${selectedUsers.size} user(s) deleted successfully`);
          setSelectedUsers(new Set());
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          fetchUsers();
        } catch (_err) {
          addToast('error', 'Failed to delete some users');
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
        }
      },
    });
  }, [selectedUsers, addToast, fetchUsers]);

  // Bulk change role
  const handleBulkChangeRole = useCallback(async () => {
    if (selectedUsers.size === 0 || !bulkRoleModal.newRole) return;

    try {
      setIsBulkProcessing(true);
      await Promise.all(
        Array.from(selectedUsers).map(id =>
          api.admin.updateUser(id, { role: bulkRoleModal.newRole })
        )
      );
      addToast('success', `Ruolo aggiornato a "${bulkRoleModal.newRole}" per ${selectedUsers.size} utenti`);
      setSelectedUsers(new Set());
      setBulkRoleModal({ isOpen: false, newRole: 'User' });
      fetchUsers();
    } catch (_err) {
      addToast('error', 'Errore nell\'aggiornamento del ruolo per alcuni utenti');
    } finally {
      setIsBulkProcessing(false);
    }
  }, [selectedUsers, bulkRoleModal.newRole, addToast, fetchUsers]);

  // Export selected users to CSV
  const handleExportCSV = useCallback(() => {
    if (selectedUsers.size === 0) return;

    const selectedUserList = users.filter(u => selectedUsers.has(u.id));
    const headers = ['ID', 'Email', 'Display Name', 'Role', 'Created At', 'Last Seen'];
    const csvRows = [
      headers.join(','),
      ...selectedUserList.map(u => [
        u.id,
        `"${u.email}"`,
        `"${u.displayName}"`,
        u.role,
        u.createdAt,
        u.lastSeenAt || 'Never'
      ].join(','))
    ];

    const csvContent = csvRows.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `users_export_${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);

    addToast('success', `${selectedUsers.size} utenti esportati in CSV`);
  }, [selectedUsers, users, addToast]);

  // Request password reset for selected users
  const _handleBulkPasswordReset = useCallback(() => {
    if (selectedUsers.size === 0) return;

    setConfirmation({
      isOpen: true,
      title: 'Reset Password per Utenti Selezionati',
      message: `Inviare un'email di reset password a ${selectedUsers.size} utente/i? Gli utenti riceveranno un link per reimpostare la password.`,
      onConfirm: async () => {
        try {
          setIsBulkProcessing(true);
          // Note: This would call a bulk password reset endpoint
          // For now we show a toast since the endpoint may not exist
          addToast('info', `Richiesta reset password inviata per ${selectedUsers.size} utenti`);
          setSelectedUsers(new Set());
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
        } catch (_err) {
          addToast('error', 'Errore nell\'invio delle email di reset password');
        } finally {
          setIsBulkProcessing(false);
        }
      },
    });
  }, [selectedUsers, addToast]);

  // Bulk suspend users
  const handleBulkSuspend = useCallback(() => {
    if (selectedUsers.size === 0) return;

    setConfirmation({
      isOpen: true,
      title: 'Suspend Multiple Users',
      message: `Are you sure you want to suspend ${selectedUsers.size} user(s)? They will not be able to login.`,
      onConfirm: async () => {
        try {
          setIsBulkProcessing(true);
          await Promise.all(
            Array.from(selectedUsers).map(id =>
              api.admin.suspendUser(id, 'Bulk suspended by admin')
            )
          );
          addToast('success', `${selectedUsers.size} user(s) suspended successfully`);
          setSelectedUsers(new Set());
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          fetchUsers();
        } catch (_err) {
          addToast('error', 'Failed to suspend some users');
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
        } finally {
          setIsBulkProcessing(false);
        }
      },
    });
  }, [selectedUsers, addToast, fetchUsers]);

  // Format date
  const _formatDate = (dateString: string | null) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleString();
  };

  // Table actions for UsersDataTable
  const tableActions: UsersTableActions = useMemo(() => ({
    onEdit: (user) => setModal({ isOpen: true, mode: 'edit', user }),
    onSuspend: (user) => handleSuspend(user.id, user.email),
    onUnsuspend: (user) => handleUnsuspend(user.id, user.email),
    onDelete: (user) => handleDelete(user.id, user.email),
  }), [handleSuspend, handleUnsuspend, handleDelete]);

  if (dataLoading) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="p-8">
          <h1>User Management</h1>
          <p data-testid="users-loading">Loading...</p>
        </div>
      </AdminAuthGuard>
    );
  }

  if (error) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="p-8">
          <h1>User Management</h1>
          <div className="p-4 bg-red-50 border border-red-600 rounded">{error}</div>
          <Link href="/admin">← Back to Admin Dashboard</Link>
        </div>
      </AdminAuthGuard>
    );
  }

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="p-8 max-w-7xl mx-auto">
        <div className="flex justify-between items-center mb-8">
          <h1>User Management</h1>
          <Link href="/admin">← Back to Admin Dashboard</Link>
        </div>

        {/* Filters */}
        <div className="mb-6 flex gap-4 flex-wrap">
          <div className="flex-[1_1_300px]">
            <input
              type="text"
              placeholder="Search by email or name..."
              value={search}
              onChange={e => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="w-full p-2 border border-gray-300 rounded"
            />
          </div>
          <div>
            <select
              value={roleFilter}
              onChange={e => {
                setRoleFilter(e.target.value);
                setPage(1);
              }}
              className="p-2 border border-gray-300 rounded"
              aria-label="Filter by role"
            >
              <option value="all">All Roles</option>
              <option value="Admin">Admin</option>
              <option value="Editor">Editor</option>
              <option value="User">User</option>
            </select>
          </div>
          <div>
            <select
              value={statusFilter}
              onChange={e => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              className="p-2 border border-gray-300 rounded"
              aria-label="Filter by status"
              data-testid="status-filter"
            >
              <option value="all">All Status</option>
              <option value="active">Active</option>
              <option value="suspended">Suspended</option>
            </select>
          </div>
        </div>

        {/* Actions */}
        <div className="mb-4 flex gap-4">
          <button
            onClick={() => setModal({ isOpen: true, mode: 'create' })}
            className="px-4 py-2 bg-blue-600 text-white border-none rounded cursor-pointer hover:bg-blue-700"
            data-testid="open-create-user-modal"
          >
            Create User
          </button>
        </div>

        {/* Bulk Action Bar */}
        <BulkActionBar
          variant="floating"
          selectedCount={selectedUsers.size}
          totalCount={users.length}
          itemLabel="users"
          itemLabelSingular="user"
          actions={[
            {
              id: 'change-role',
              label: 'Change Role',
              icon: Shield,
              variant: 'outline',
              onClick: () => setBulkRoleModal({ isOpen: true, newRole: 'User' }),
              tooltip: 'Change role for selected users',
              disabled: isBulkProcessing,
            },
            {
              id: 'export',
              label: 'Export CSV',
              icon: Download,
              variant: 'outline',
              onClick: () => handleExportCSV(),
              tooltip: 'Export selected users to CSV',
              showCount: false,
            },
            {
              id: 'suspend',
              label: 'Suspend',
              icon: Ban,
              variant: 'secondary',
              onClick: () => handleBulkSuspend(),
              tooltip: 'Suspend selected users',
              disabled: isBulkProcessing,
            },
            {
              id: 'delete',
              label: 'Delete',
              icon: Trash2,
              variant: 'destructive',
              onClick: () => handleBulkDelete(),
              tooltip: 'Delete selected users',
              disabled: isBulkProcessing,
            },
          ]}
          onClearSelection={() => setSelectedUsers(new Set())}
        />

        {/* User Table - TanStack Table */}
        <div className="mb-4">
          <UsersDataTable
            users={users}
            isLoading={dataLoading}
            sorting={sorting}
            onSortingChange={setSorting}
            rowSelection={rowSelection}
            onRowSelectionChange={handleRowSelectionChange}
            actions={tableActions}
          />
        </div>

        {/* Pagination */}
        <div className="flex justify-between items-center">
          <div className="text-gray-600 text-sm">
            Showing {Math.min((page - 1) * pageSize + 1, total)} to{' '}
            {Math.min(page * pageSize, total)} of {total} users
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => setPage(Math.max(1, page - 1))}
              disabled={page === 1}
              className={cn(
                'px-4 py-2 border-none rounded',
                page === 1
                  ? 'bg-gray-200 text-gray-500 cursor-not-allowed'
                  : 'bg-blue-600 text-white cursor-pointer hover:bg-blue-700'
              )}
              data-testid="pagination-previous"
            >
              Previous
            </button>
            <span className="px-4 py-2 flex items-center">
              Page {page} of {Math.ceil(total / pageSize)}
            </span>
            <button
              onClick={() => setPage(page + 1)}
              disabled={page >= Math.ceil(total / pageSize)}
              className={cn(
                'px-4 py-2 border-none rounded',
                page >= Math.ceil(total / pageSize)
                  ? 'bg-gray-200 text-gray-500 cursor-not-allowed'
                  : 'bg-blue-600 text-white cursor-pointer hover:bg-blue-700'
              )}
              data-testid="pagination-next"
            >
              Next
            </button>
          </div>
        </div>

        {/* User Modal (Create/Edit) */}
        {modal.isOpen && (
          <UserModal
            mode={modal.mode}
            user={modal.user}
            onClose={() => setModal({ isOpen: false, mode: 'create' })}
            onCreate={handleCreate}
            onUpdate={handleUpdate}
          />
        )}

        {/* Confirmation Dialog */}
        {confirmation.isOpen && (
          <div
            className="fixed inset-0 bg-black/50 flex justify-center items-center z-[1000]"
            onClick={() =>
              setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} })
            }
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirmation-title"
          >
            <div
              className="bg-white p-8 rounded-lg max-w-lg w-[90%]"
              onClick={e => e.stopPropagation()}
            >
              <h2 id="confirmation-title" className="mt-0">
                {confirmation.title}
              </h2>
              <p>{confirmation.message}</p>
              <div className="flex justify-end gap-4 mt-6">
                <button
                  onClick={() =>
                    setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} })
                  }
                  className="px-4 py-2 bg-gray-600 text-white border-none rounded cursor-pointer hover:bg-gray-700"
                >
                  Cancel
                </button>
                <button
                  onClick={confirmation.onConfirm}
                  className="px-4 py-2 bg-red-600 text-white border-none rounded cursor-pointer hover:bg-red-700"
                >
                  Confirm
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Bulk Role Change Modal */}
        {bulkRoleModal.isOpen && (
          <div
            className="fixed inset-0 bg-black/50 flex justify-center items-center z-[1000]"
            onClick={() => setBulkRoleModal({ isOpen: false, newRole: 'User' })}
            role="dialog"
            aria-modal="true"
            aria-labelledby="bulk-role-title"
          >
            <div
              className="bg-white p-8 rounded-lg max-w-md w-[90%]"
              onClick={e => e.stopPropagation()}
            >
              <h2 id="bulk-role-title" className="mt-0">
                Cambia Ruolo - {selectedUsers.size} Utenti
              </h2>
              <p className="text-gray-600 mb-4">
                Seleziona il nuovo ruolo da assegnare a {selectedUsers.size} utente/i selezionati.
              </p>

              <div className="mb-6">
                <label htmlFor="bulk-role-select" className="block mb-2 font-medium">
                  Nuovo Ruolo
                </label>
                <select
                  id="bulk-role-select"
                  value={bulkRoleModal.newRole}
                  onChange={e => setBulkRoleModal(prev => ({ ...prev, newRole: e.target.value }))}
                  className="w-full p-2 border border-gray-300 rounded"
                  disabled={isBulkProcessing}
                >
                  <option value="User">User</option>
                  <option value="Editor">Editor</option>
                  <option value="Admin">Admin</option>
                </select>
              </div>

              <div className="flex justify-end gap-4">
                <button
                  onClick={() => setBulkRoleModal({ isOpen: false, newRole: 'User' })}
                  className="px-4 py-2 bg-gray-600 text-white border-none rounded cursor-pointer hover:bg-gray-700"
                  disabled={isBulkProcessing}
                >
                  Annulla
                </button>
                <button
                  onClick={handleBulkChangeRole}
                  className="px-4 py-2 bg-blue-600 text-white border-none rounded cursor-pointer hover:bg-blue-700 disabled:bg-blue-300"
                  disabled={isBulkProcessing}
                >
                  {isBulkProcessing ? 'Applicando...' : 'Applica Ruolo'}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Toast Notifications */}
        <div className="fixed top-4 right-4 z-[1001]">
          {toasts?.map(toast => (
            <div
              key={toast.id}
              className={cn(
                'p-4 mb-2 rounded max-w-md flex justify-between items-center',
                toast.type === 'success' && 'bg-green-100 border border-green-300 text-green-800',
                toast.type === 'error' && 'bg-red-100 border border-red-300 text-red-800',
                toast.type === 'info' && 'bg-blue-100 border border-blue-300 text-blue-800'
              )}
            >
              <span>{toast.message}</span>
              <button
                onClick={() => removeToast(toast.id)}
                className="bg-transparent border-none cursor-pointer text-xl ml-4"
                style={{ color: 'inherit' }}
                aria-label="Close notification"
              >
                ×
              </button>
            </div>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}

// User Modal Component
type UserModalProps = {
  mode: 'create' | 'edit';
  user?: User;
  onClose: () => void;
  onCreate: (userData: CreateUserRequest) => void;
  onUpdate: (userId: string, updates: UpdateUserRequest) => void;
};

function UserModal({ mode, user, onClose, onCreate, onUpdate }: UserModalProps) {
  const [email, setEmail] = useState(user?.email || '');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState(user?.displayName || '');
  const [role, setRole] = useState(user?.role || 'User');
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!email || !email.includes('@')) {
      newErrors.email = 'Valid email is required';
    }

    if (mode === 'create' && (!password || password.length < 8)) {
      newErrors.password = 'Password must be at least 8 characters';
    }

    if (!displayName.trim()) {
      newErrors.displayName = 'Display name is required';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!validate()) return;

    if (mode === 'create') {
      onCreate({ email, password, displayName, role });
    } else if (user) {
      const updates: UpdateUserRequest = {};
      if (email !== user.email) updates.email = email;
      if (displayName !== user.displayName) updates.displayName = displayName;
      if (role !== user.role) updates.role = role;

      if (Object.keys(updates).length > 0) {
        onUpdate(user.id, updates);
      } else {
        onClose();
      }
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex justify-center items-center z-[1000]"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
    >
      <div className="bg-white p-8 rounded-lg max-w-lg w-[90%]" onClick={e => e.stopPropagation()}>
        <h2 id="modal-title" className="mt-0" data-testid="user-modal-title">
          {mode === 'create' ? 'Create User' : 'Edit User'}
        </h2>

        <form noValidate onSubmit={handleSubmit}>
          {/* Email */}
          <div className="mb-4">
            <label htmlFor="email" className="block mb-2 font-medium">
              Email *
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              className={cn(
                'w-full p-2 rounded',
                errors.email ? 'border border-red-600' : 'border border-gray-300'
              )}
              required
            />
            {errors.email && <div className="text-red-600 text-sm mt-1">{errors.email}</div>}
          </div>

          {/* Password (only for create) */}
          {mode === 'create' && (
            <div className="mb-4">
              <label htmlFor="password" className="block mb-2 font-medium">
                Password *
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                className={cn(
                  'w-full p-2 rounded',
                  errors.password ? 'border border-red-600' : 'border border-gray-300'
                )}
                required
                minLength={8}
              />
              {errors.password && (
                <div className="text-red-600 text-sm mt-1">{errors.password}</div>
              )}
            </div>
          )}

          {/* Display Name */}
          <div className="mb-4">
            <label htmlFor="displayName" className="block mb-2 font-medium">
              Display Name *
            </label>
            <input
              id="displayName"
              type="text"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              className={cn(
                'w-full p-2 rounded',
                errors.displayName ? 'border border-red-600' : 'border border-gray-300'
              )}
              required
            />
            {errors.displayName && (
              <div className="text-red-600 text-sm mt-1">{errors.displayName}</div>
            )}
          </div>

          {/* Role */}
          <div className="mb-6">
            <label htmlFor="role" className="block mb-2 font-medium">
              Role *
            </label>
            <select
              id="role"
              value={role}
              onChange={e => setRole(e.target.value as UserRole)}
              className="w-full p-2 border border-gray-300 rounded"
              required
            >
              <option value="User">User</option>
              <option value="Editor">Editor</option>
              <option value="Admin">Admin</option>
            </select>
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-4">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 bg-gray-600 text-white border-none rounded cursor-pointer hover:bg-gray-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-blue-600 text-white border-none rounded cursor-pointer hover:bg-blue-700"
              data-testid="submit-user-form"
            >
              {mode === 'create' ? 'Create User' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
