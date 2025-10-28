import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../../lib/api";

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

export default function UserManagement() {
  // State
  const [users, setUsers] = useState<User[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [sortBy, setSortBy] = useState("createdAt");
  const [sortOrder, setSortOrder] = useState<"asc" | "desc">("desc");
  const [loading, setLoading] = useState(true);
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
      setLoading(true);
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
      setLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "An error occurred");
      setLoading(false);
      addToast("error", "Failed to load users");
    }
  }, [page, pageSize, search, roleFilter, sortBy, sortOrder, addToast]);

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
  const handleSort = useCallback(
    (field: string) => {
      if (sortBy === field) {
        setSortOrder(sortOrder === "asc" ? "desc" : "asc");
      } else {
        setSortBy(field);
        setSortOrder("asc");
      }
    },
    [sortBy, sortOrder]
  );

  // Format date
  const formatDate = (dateString: string | null) => {
    if (!dateString) return "Never";
    return new Date(dateString).toLocaleString();
  };

  if (loading) {
    return (
      <div style={{ padding: "2rem" }}>
        <h1>User Management</h1>
        <p>Loading...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div style={{ padding: "2rem" }}>
        <h1>User Management</h1>
        <div style={{ padding: "1rem", background: "#fee", border: "1px solid #c00", borderRadius: "4px" }}>
          {error}
        </div>
        <Link href="/admin">← Back to Admin Dashboard</Link>
      </div>
    );
  }

  return (
    <div style={{ padding: "2rem", maxWidth: "1400px", margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "2rem" }}>
        <h1>User Management</h1>
        <Link href="/admin">← Back to Admin Dashboard</Link>
      </div>

      {/* Filters */}
      <div style={{ marginBottom: "1.5rem", display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <div style={{ flex: "1 1 300px" }}>
          <input
            type="text"
            placeholder="Search by email or name..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            style={{
              width: "100%",
              padding: "0.5rem",
              border: "1px solid #ccc",
              borderRadius: "4px",
            }}
          />
        </div>
        <div>
          <select
            value={roleFilter}
            onChange={(e) => {
              setRoleFilter(e.target.value);
              setPage(1);
            }}
            style={{
              padding: "0.5rem",
              border: "1px solid #ccc",
              borderRadius: "4px",
            }}
          >
            <option value="all">All Roles</option>
            <option value="Admin">Admin</option>
            <option value="Editor">Editor</option>
            <option value="User">User</option>
          </select>
        </div>
      </div>

      {/* Actions */}
      <div style={{ marginBottom: "1rem", display: "flex", gap: "1rem" }}>
        <button
          onClick={() => setModal({ isOpen: true, mode: "create" })}
          style={{
            padding: "0.5rem 1rem",
            background: "#007bff",
            color: "white",
            border: "none",
            borderRadius: "4px",
            cursor: "pointer",
          }}
        >
          Create User
        </button>
        {selectedUsers.size > 0 && (
          <button
            onClick={handleBulkDelete}
            style={{
              padding: "0.5rem 1rem",
              background: "#dc3545",
              color: "white",
              border: "none",
              borderRadius: "4px",
              cursor: "pointer",
            }}
          >
            Delete Selected ({selectedUsers.size})
          </button>
        )}
      </div>

      {/* User Table */}
      <div style={{ overflowX: "auto", marginBottom: "1rem" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", border: "1px solid #ddd" }}>
          <thead style={{ background: "#f8f9fa" }}>
            <tr>
              <th style={{ padding: "0.75rem", borderBottom: "2px solid #dee2e6", width: "40px" }}>
                <input
                  type="checkbox"
                  checked={selectedUsers.size === users.length && users.length > 0}
                  onChange={toggleAllUsers}
                  aria-label="Select all users"
                />
              </th>
              <th
                style={{
                  padding: "0.75rem",
                  borderBottom: "2px solid #dee2e6",
                  cursor: "pointer",
                  textAlign: "left",
                }}
                onClick={() => handleSort("email")}
              >
                Email {sortBy === "email" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th
                style={{
                  padding: "0.75rem",
                  borderBottom: "2px solid #dee2e6",
                  cursor: "pointer",
                  textAlign: "left",
                }}
                onClick={() => handleSort("displayName")}
              >
                Display Name {sortBy === "displayName" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th
                style={{
                  padding: "0.75rem",
                  borderBottom: "2px solid #dee2e6",
                  cursor: "pointer",
                  textAlign: "left",
                }}
                onClick={() => handleSort("role")}
              >
                Role {sortBy === "role" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th
                style={{
                  padding: "0.75rem",
                  borderBottom: "2px solid #dee2e6",
                  cursor: "pointer",
                  textAlign: "left",
                }}
                onClick={() => handleSort("createdAt")}
              >
                Created {sortBy === "createdAt" && (sortOrder === "asc" ? "↑" : "↓")}
              </th>
              <th style={{ padding: "0.75rem", borderBottom: "2px solid #dee2e6", textAlign: "left" }}>
                Last Seen
              </th>
              <th style={{ padding: "0.75rem", borderBottom: "2px solid #dee2e6", textAlign: "center" }}>
                Actions
              </th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ padding: "2rem", textAlign: "center", color: "#666" }}>
                  No users found
                </td>
              </tr>
            ) : (
              users.map((user) => (
                <tr key={user.id} style={{ borderBottom: "1px solid #dee2e6" }}>
                  <td style={{ padding: "0.75rem" }}>
                    <input
                      type="checkbox"
                      checked={selectedUsers.has(user.id)}
                      onChange={() => toggleUserSelection(user.id)}
                      aria-label={`Select ${user.email}`}
                    />
                  </td>
                  <td style={{ padding: "0.75rem" }}>{user.email}</td>
                  <td style={{ padding: "0.75rem" }}>{user.displayName}</td>
                  <td style={{ padding: "0.75rem" }}>
                    <span
                      style={{
                        padding: "0.25rem 0.5rem",
                        borderRadius: "4px",
                        fontSize: "0.875rem",
                        background:
                          user.role === "Admin"
                            ? "#dc3545"
                            : user.role === "Editor"
                            ? "#ffc107"
                            : "#28a745",
                        color: user.role === "Editor" ? "#000" : "#fff",
                      }}
                    >
                      {user.role}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem", fontSize: "0.875rem", color: "#666" }}>
                    {formatDate(user.createdAt)}
                  </td>
                  <td style={{ padding: "0.75rem", fontSize: "0.875rem", color: "#666" }}>
                    {formatDate(user.lastSeenAt)}
                  </td>
                  <td style={{ padding: "0.75rem", textAlign: "center" }}>
                    <button
                      onClick={() => setModal({ isOpen: true, mode: "edit", user })}
                      style={{
                        padding: "0.25rem 0.75rem",
                        marginRight: "0.5rem",
                        background: "#007bff",
                        color: "white",
                        border: "none",
                        borderRadius: "4px",
                        cursor: "pointer",
                        fontSize: "0.875rem",
                      }}
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(user.id, user.email)}
                      style={{
                        padding: "0.25rem 0.75rem",
                        background: "#dc3545",
                        color: "white",
                        border: "none",
                        borderRadius: "4px",
                        cursor: "pointer",
                        fontSize: "0.875rem",
                      }}
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
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div style={{ color: "#666", fontSize: "0.875rem" }}>
          Showing {Math.min((page - 1) * pageSize + 1, total)} to {Math.min(page * pageSize, total)} of {total} users
        </div>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <button
            onClick={() => setPage(Math.max(1, page - 1))}
            disabled={page === 1}
            style={{
              padding: "0.5rem 1rem",
              background: page === 1 ? "#e9ecef" : "#007bff",
              color: page === 1 ? "#6c757d" : "white",
              border: "none",
              borderRadius: "4px",
              cursor: page === 1 ? "not-allowed" : "pointer",
            }}
          >
            Previous
          </button>
          <span style={{ padding: "0.5rem 1rem", display: "flex", alignItems: "center" }}>
            Page {page} of {Math.ceil(total / pageSize)}
          </span>
          <button
            onClick={() => setPage(page + 1)}
            disabled={page >= Math.ceil(total / pageSize)}
            style={{
              padding: "0.5rem 1rem",
              background: page >= Math.ceil(total / pageSize) ? "#e9ecef" : "#007bff",
              color: page >= Math.ceil(total / pageSize) ? "#6c757d" : "white",
              border: "none",
              borderRadius: "4px",
              cursor: page >= Math.ceil(total / pageSize) ? "not-allowed" : "pointer",
            }}
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
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: "rgba(0, 0, 0, 0.5)",
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            zIndex: 1000,
          }}
          onClick={() => setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} })}
        >
          <div
            style={{
              background: "white",
              padding: "2rem",
              borderRadius: "8px",
              maxWidth: "500px",
              width: "90%",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <h2 style={{ marginTop: 0 }}>{confirmation.title}</h2>
            <p>{confirmation.message}</p>
            <div style={{ display: "flex", justifyContent: "flex-end", gap: "1rem", marginTop: "1.5rem" }}>
              <button
                onClick={() => setConfirmation({ isOpen: false, title: "", message: "", onConfirm: () => {} })}
                style={{
                  padding: "0.5rem 1rem",
                  background: "#6c757d",
                  color: "white",
                  border: "none",
                  borderRadius: "4px",
                  cursor: "pointer",
                }}
              >
                Cancel
              </button>
              <button
                onClick={confirmation.onConfirm}
                style={{
                  padding: "0.5rem 1rem",
                  background: "#dc3545",
                  color: "white",
                  border: "none",
                  borderRadius: "4px",
                  cursor: "pointer",
                }}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toast Notifications */}
      <div style={{ position: "fixed", top: "1rem", right: "1rem", zIndex: 1001 }}>
        {toasts.map((toast) => (
          <div
            key={toast.id}
            style={{
              padding: "1rem",
              marginBottom: "0.5rem",
              background: toast.type === "success" ? "#d4edda" : toast.type === "error" ? "#f8d7da" : "#d1ecf1",
              border: `1px solid ${
                toast.type === "success" ? "#c3e6cb" : toast.type === "error" ? "#f5c6cb" : "#bee5eb"
              }`,
              borderRadius: "4px",
              color: toast.type === "success" ? "#155724" : toast.type === "error" ? "#721c24" : "#0c5460",
              maxWidth: "400px",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <span>{toast.message}</span>
            <button
              onClick={() => removeToast(toast.id)}
              style={{
                background: "none",
                border: "none",
                cursor: "pointer",
                fontSize: "1.25rem",
                marginLeft: "1rem",
                color: "inherit",
              }}
              aria-label="Close notification"
            >
              ×
            </button>
          </div>
        ))}
      </div>
    </div>
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
      style={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: "rgba(0, 0, 0, 0.5)",
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        zIndex: 1000,
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: "white",
          padding: "2rem",
          borderRadius: "8px",
          maxWidth: "500px",
          width: "90%",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 style={{ marginTop: 0 }}>{mode === "create" ? "Create User" : "Edit User"}</h2>

        <form onSubmit={handleSubmit}>
          {/* Email */}
          <div style={{ marginBottom: "1rem" }}>
            <label htmlFor="email" style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>
              Email *
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem",
                border: errors.email ? "1px solid #dc3545" : "1px solid #ccc",
                borderRadius: "4px",
              }}
              required
            />
            {errors.email && <div style={{ color: "#dc3545", fontSize: "0.875rem", marginTop: "0.25rem" }}>{errors.email}</div>}
          </div>

          {/* Password (only for create) */}
          {mode === "create" && (
            <div style={{ marginBottom: "1rem" }}>
              <label htmlFor="password" style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>
                Password *
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                style={{
                  width: "100%",
                  padding: "0.5rem",
                  border: errors.password ? "1px solid #dc3545" : "1px solid #ccc",
                  borderRadius: "4px",
                }}
                required
                minLength={8}
              />
              {errors.password && <div style={{ color: "#dc3545", fontSize: "0.875rem", marginTop: "0.25rem" }}>{errors.password}</div>}
            </div>
          )}

          {/* Display Name */}
          <div style={{ marginBottom: "1rem" }}>
            <label htmlFor="displayName" style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>
              Display Name *
            </label>
            <input
              id="displayName"
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem",
                border: errors.displayName ? "1px solid #dc3545" : "1px solid #ccc",
                borderRadius: "4px",
              }}
              required
            />
            {errors.displayName && <div style={{ color: "#dc3545", fontSize: "0.875rem", marginTop: "0.25rem" }}>{errors.displayName}</div>}
          </div>

          {/* Role */}
          <div style={{ marginBottom: "1.5rem" }}>
            <label htmlFor="role" style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>
              Role *
            </label>
            <select
              id="role"
              value={role}
              onChange={(e) => setRole(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem",
                border: "1px solid #ccc",
                borderRadius: "4px",
              }}
              required
            >
              <option value="User">User</option>
              <option value="Editor">Editor</option>
              <option value="Admin">Admin</option>
            </select>
          </div>

          {/* Actions */}
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "1rem" }}>
            <button
              type="button"
              onClick={onClose}
              style={{
                padding: "0.5rem 1rem",
                background: "#6c757d",
                color: "white",
                border: "none",
                borderRadius: "4px",
                cursor: "pointer",
              }}
            >
              Cancel
            </button>
            <button
              type="submit"
              style={{
                padding: "0.5rem 1rem",
                background: "#007bff",
                color: "white",
                border: "none",
                borderRadius: "4px",
                cursor: "pointer",
              }}
            >
              {mode === "create" ? "Create User" : "Save Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
