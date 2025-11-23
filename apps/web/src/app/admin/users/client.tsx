'use client';

import type { AuthUser } from '@/types/auth';
import { useCallback, useEffect, useState, startTransition } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { useAuthUser } from '@/components/auth/AuthProvider';
import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';

// Types
type User = {
  id: string;
  email: string;
  displayName: string;
  role: string;
  createdAt: string;
  lastSeenAt: string | null;
};

type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

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
  type: "success" | "error" | "info";
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
  mode: "create" | "edit";
  user?: User;
};

export function AdminPageClient() {
  const { user, loading: authLoading } = useAuthUser();

  if (!user) return null;
  // State
  const [users, setUsers] = useState<User[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [sortBy, setSortBy] = useState("createdAt");
  const [sortOrder, setSortOrder] = useState<"asc" | "desc">("desc");
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [confirmation, setConfirmation] = useState<ConfirmationDialog>({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: () => {},
  });
  const [modal, setModal] = useState<ModalState>({
    isOpen: false,
    mode: "create",
  });
  const [selectedUsers, setSelectedUsers] = useState<Set<string>>(new Set());

  // Toast management
  const addToast = useCallback((type: "success" | "error" | "info", message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts((prev) => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 5000);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  // Fetch users
  const fetchUsers = useCallback(async () => {
    try {
      setDataLoading(true);
      setError(null);

      const queryParams = new URLSearchParams({
        page: page.toString(),
        limit: pageSize.toString(),
        sortBy,
        sortOrder,
      });

      if (search) {
        queryParams.append("search", search);
      }
      if (roleFilter !== "all") {
        queryParams.append("role", roleFilter);
      }

      const result = await api.get<PagedResult<User>>(
        `/api/v1/admin/users?${queryParams.toString()}`
      );

      if (!result) {
        throw new Error("Unauthorized - Admin access required");
      }

      setUsers(result.items);
      setTotal(result.total);
      setDataLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "An error occurred");
      setDataLoading(false);
      addToast("error", "Failed to load users");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- addToast is stable (defined with useCallback and empty deps)
  }, [page, pageSize, search, roleFilter, sortBy, sortOrder]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  // Create user
  const handleCreate = useCallback(
    async (userData: CreateUserRequest) => {
      try {
        await api.post("/api/v1/admin/users", userData);
        addToast("success", `User ${userData.email} created successfully`);
        setModal({ isOpen: false, mode: "create" });
        fetchUsers();
      } catch (err) {
        addToast("error", err instanceof Error ? err.message : "Failed to create user");
      }
    },
    [addToast, fetchUsers]
  );

  // Update user
  const handleUpdate = useCallback(
    async (userId: string, updates: UpdateUserRequest) => {
      try {
        await api.put(`/api/v1/admin/users/${userId}`, updates);
        addToast("success", "User updated successfully");
        setModal({ isOpen: false, mode: "create" });
        fetchUsers();
      } catch (err) {
        addToast("error", err instanceof Error ? err.message : "Failed to update user");
      }
    },
    [addToast, fetchUsers]
  );

  // Delete user
  const handleDelete = useCallback(
    async (userId: string, email: string) => {
      setConfirmation({
        isOpen: true,
        title: "Delete User",
        message: `Are you sure you want to delete ${email}? This action cannot be undone.`,
        onConfirm: async () => {
          try {
            await api.delete(`/api/v1/admin/users/${userId}`);
            addToast("success", `User ${email} deleted successfully`);
            setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} });
            fetchUsers();
          } catch (err) {
            addToast("error", err instanceof Error ? err.message : "Failed to delete user");
            setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} });
          }
        },
      });
    },
    [addToast, fetchUsers]
  );

  // Bulk delete
  const handleBulkDelete = useCallback(() => {
    if (selectedUsers.size === 0) return;

    setConfirmation({
      isOpen: true,
      title: "Delete Multiple Users",
      message: `Are you sure you want to delete ${selectedUsers.size} user(s)? This action cannot be undone.`,
      onConfirm: async () => {
        try {
          await Promise.all(
            Array.from(selectedUsers).map((id) => api.delete(`/api/v1/admin/users/${id}`))
          );
          addToast("success", `${selectedUsers.size} user(s) deleted successfully`);
          setSelectedUsers(new Set());
          setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} });
          fetchUsers();
        } catch (err) {
          addToast("error", "Failed to delete some users");
          setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} });
        }
      },
    });
  }, [selectedUsers, addToast, fetchUsers]);

  // Toggle user selection
  const toggleUserSelection = useCallback((userId: string) => {
    setSelectedUsers((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(userId)) {
        newSet.delete(userId);
      } else {
        newSet.add(userId);
      }
      return newSet;
    });
  }, []);

  // Toggle all users
  const toggleAllUsers = useCallback(() => {
    if (selectedUsers.size === users.length) {
      setSelectedUsers(new Set());
    } else {
      setSelectedUsers(new Set(users.map((u) => u.id)));
    }
  }, [users, selectedUsers.size]);

  // Handle sort
  const handleSort = useCallback((field: string) => {
    setSortBy(prevSortBy => {
      if (prevSortBy === field) {
        // Toggling same column - toggle order and keep same field
        setSortOrder(prevOrder => prevOrder === "asc" ? "desc" : "asc");
        return prevSortBy;
      } else {
        // New column - reset to ascending
        setSortOrder("asc");
        return field;
      }
    });
  }, []);

  // Format date
  const formatDate = (dateString: string | null) => {
    if (!dateString) return "Never";
    return new Date(dateString).toLocaleString();
  };

  if (dataLoading) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="p-8">
          <h1>User Management</h1>
          <p>Loading...</p>
        </div>
      </AdminAuthGuard>
    );
  }

  if (error) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="p-8">
          <h1>User Management</h1>
          <div className="p-4 bg-red-50 border border-red-600 rounded">
            {error}
          </div>
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
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="w-full p-2 border border-gray-300 rounded"
          />
        </div>
        <div>
          <select
            value={roleFilter}
            onChange={(e) => {
              setRoleFilter(e.target.value);
              setPage(1);
            }}
            className="p-2 border border-gray-300 rounded"
          >
            <option value="all">All Roles</option>
            <option value="Admin">Admin</option>
            <option value="Editor">Editor</option>
            <option value="User">User</option>
          </select>
        </div>
      </div>

      {/* Actions */}
      <div className="mb-4 flex gap-4">
        <button
          onClick={() => setModal({ isOpen: true, mode: "create" })}
          className="px-4 py-2 bg-blue-600 text-white border-none rounded cursor-pointer hover:bg-blue-700"
          data-testid="open-create-user-modal"
        >
          Create User
        </button>
        {selectedUsers.size > 0 && (
          <button
            onClick={handleBulkDelete}
            className="px-4 py-2 bg-red-600 text-white border-none rounded cursor-pointer hover:bg-red-700"
          >
            Delete Selected ({selectedUsers.size})
          </button>
        )}
      </div>

      {/* User Table */}
      <div className="overflow-x-auto mb-4">
        <table className="w-full border-collapse border border-gray-300">
          <thead className="bg-gray-50">
            <tr>
              <th className="p-3 border-b-2 border-gray-300 w-10">
                <input
                  type="checkbox"
                  checked={selectedUsers.size === users.length && users.length > 0}
                  onChange={toggleAllUsers}
                  aria-label="Select all users"
                />
              </th>
              <th
                className="p-3 border-b-2 border-gray-300 cursor-pointer text-left"
                onClick={() => handleSort("email")}
              >
                Email {sortBy === "email" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th
                className="p-3 border-b-2 border-gray-300 cursor-pointer text-left"
                onClick={() => handleSort("displayName")}
              >
                Display Name {sortBy === "displayName" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th
                className="p-3 border-b-2 border-gray-300 cursor-pointer text-left"
                onClick={() => handleSort("role")}
              >
                Role {sortBy === "role" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th
                className="p-3 border-b-2 border-gray-300 cursor-pointer text-left"
                onClick={() => handleSort("createdAt")}
              >
                Created {sortBy === "createdAt" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th className="p-3 border-b-2 border-gray-300 text-left">
                Last Seen
              </th>
              <th className="p-3 border-b-2 border-gray-300 text-center">
                Actions
              </th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr>
                <td colSpan={7} className="p-8 text-center text-gray-600">
                  No users found
                </td>
              </tr>
            ) : (
              users.map((user) => (
                <tr key={user.id} className="border-b border-gray-300">
                  <td className="p-3">
                    <input
                      type="checkbox"
                      checked={selectedUsers.has(user.id)}
                      onChange={() => toggleUserSelection(user.id)}
                      aria-label={`Select ${user.email}`}
                    />
                  </td>
                  <td className="p-3">{user.email}</td>
                  <td className="p-3">{user.displayName}</td>
                  <td className="p-3">
                    <span
                      className={cn(
                        "px-2 py-1 rounded text-sm",
                        user.role === "Admin" && "bg-red-600 text-white",
                        user.role === "Editor" && "bg-yellow-400 text-black",
                        user.role === "User" && "bg-green-600 text-white"
                      )}
                    >
                      {user.role}
                    </span>
                  </td>
                  <td className="p-3 text-sm text-gray-600">
                    {formatDate(user.createdAt)}
                  </td>
                  <td className="p-3 text-sm text-gray-600">
                    {formatDate(user.lastSeenAt)}
                  </td>
                  <td className="p-3 text-center">
                    <button
                      onClick={() => setModal({ isOpen: true, mode: "edit", user })}
                      className="px-3 py-1 mr-2 bg-blue-600 text-white border-none rounded cursor-pointer text-sm hover:bg-blue-700"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(user.id, user.email)}
                      className="px-3 py-1 bg-red-600 text-white border-none rounded cursor-pointer text-sm hover:bg-red-700"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex justify-between items-center">
        <div className="text-gray-600 text-sm">
          Showing {Math.min((page - 1) * pageSize + 1, total)} to {Math.min(page * pageSize, total)} of {total} users
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setPage(Math.max(1, page - 1))}
            disabled={page === 1}
            className={cn(
              "px-4 py-2 border-none rounded",
              page === 1
                ? "bg-gray-200 text-gray-500 cursor-not-allowed"
                : "bg-blue-600 text-white cursor-pointer hover:bg-blue-700"
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
              "px-4 py-2 border-none rounded",
              page >= Math.ceil(total / pageSize)
                ? "bg-gray-200 text-gray-500 cursor-not-allowed"
                : "bg-blue-600 text-white cursor-pointer hover:bg-blue-700"
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
          onClose={() => setModal({ isOpen: false, mode: "create" })}
          onCreate={handleCreate}
          onUpdate={handleUpdate}
        />
      )}

      {/* Confirmation Dialog */}
      {confirmation.isOpen && (
        <div
          className="fixed inset-0 bg-black/50 flex justify-center items-center z-[1000]"
          onClick={() => setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} })}
        >
          <div
            className="bg-white p-8 rounded-lg max-w-lg w-[90%]"
            onClick={(e) => e.stopPropagation()}
          >
            <h2 className="mt-0">{confirmation.title}</h2>
            <p>{confirmation.message}</p>
            <div className="flex justify-end gap-4 mt-6">
              <button
                onClick={() => setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} })}
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

      {/* Toast Notifications */}
      <div className="fixed top-4 right-4 z-[1001]">
        {toasts?.map((toast) => (
          <div
            key={toast.id}
            className={cn(
              "p-4 mb-2 rounded max-w-md flex justify-between items-center",
              toast.type === "success" && "bg-green-100 border border-green-300 text-green-800",
              toast.type === "error" && "bg-red-100 border border-red-300 text-red-800",
              toast.type === "info" && "bg-blue-100 border border-blue-300 text-blue-800"
            )}
          >
            <span>{toast.message}</span>
            <button
              onClick={() => removeToast(toast.id)}
              className="bg-transparent border-none cursor-pointer text-xl ml-4"
              style={{ color: "inherit" }}
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
  mode: "create" | "edit";
  user?: User;
  onClose: () => void;
  onCreate: (userData: CreateUserRequest) => void;
  onUpdate: (userId: string, updates: UpdateUserRequest) => void;
};

function UserModal({ mode, user, onClose, onCreate, onUpdate }: UserModalProps) {
  const [email, setEmail] = useState(user?.email || "");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState(user?.displayName || "");
  const [role, setRole] = useState(user?.role || "User");
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!email || !email.includes("@")) {
      newErrors.email = "Valid email is required";
    }

    if (mode === "create" && (!password || password.length < 8)) {
      newErrors.password = "Password must be at least 8 characters";
    }

    if (!displayName.trim()) {
      newErrors.displayName = "Display name is required";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!validate()) return;

    if (mode === "create") {
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
    >
      <div
        className="bg-white p-8 rounded-lg max-w-lg w-[90%]"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="mt-0">{mode === "create" ? "Create User" : "Edit User"}</h2>

        <form onSubmit={handleSubmit}>
          {/* Email */}
          <div className="mb-4">
            <label htmlFor="email" className="block mb-2 font-medium">
              Email *
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className={cn(
                "w-full p-2 rounded",
                errors.email ? "border border-red-600" : "border border-gray-300"
              )}
              required
            />
            {errors.email && <div className="text-red-600 text-sm mt-1">{errors.email}</div>}
          </div>

          {/* Password (only for create) */}
          {mode === "create" && (
            <div className="mb-4">
              <label htmlFor="password" className="block mb-2 font-medium">
                Password *
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className={cn(
                  "w-full p-2 rounded",
                  errors.password ? "border border-red-600" : "border border-gray-300"
                )}
                required
                minLength={8}
              />
              {errors.password && <div className="text-red-600 text-sm mt-1">{errors.password}</div>}
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
              onChange={(e) => setDisplayName(e.target.value)}
              className={cn(
                "w-full p-2 rounded",
                errors.displayName ? "border border-red-600" : "border border-gray-300"
              )}
              required
            />
            {errors.displayName && <div className="text-red-600 text-sm mt-1">{errors.displayName}</div>}
          </div>

          {/* Role */}
          <div className="mb-6">
            <label htmlFor="role" className="block mb-2 font-medium">
              Role *
            </label>
            <select
              id="role"
              value={role}
              onChange={(e) => setRole(e.target.value)}
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
              {mode === "create" ? "Create User" : "Save Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
