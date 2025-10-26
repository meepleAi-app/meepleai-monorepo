import React, { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/router";
import { api } from "../../../lib/api";

type PromptTemplate = {
  id: string;
  name: string;
  description: string;
  category: string;
  createdAt: string;
  updatedAt: string;
  activeVersionId?: string | null;
};

type PagedResult = {
  templates: PromptTemplate[];
  totalPages: number;
  page: number;
  total: number;
};

type ModalState = {
  isOpen: boolean;
  mode: "create" | "edit";
  template?: PromptTemplate;
};

type ToastState = {
  show: boolean;
  message: string;
  type: "success" | "error";
};

type DeleteDialogState = {
  isOpen: boolean;
  template?: PromptTemplate;
};

const CATEGORIES = [
  "qa-system-prompt",
  "chess-system-prompt",
  "setup-guide-system-prompt",
  "streaming-qa-prompt",
];

export default function AdminPrompts() {
  const router = useRouter();

  // Data state
  const [templates, setTemplates] = useState<PromptTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter/pagination state
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [sortBy, setSortBy] = useState("createdAt");
  const [sortOrder, setSortOrder] = useState<"asc" | "desc">("desc");

  // Modal/dialog state
  const [modalState, setModalState] = useState<ModalState>({
    isOpen: false,
    mode: "create",
  });
  const [deleteDialog, setDeleteDialog] = useState<DeleteDialogState>({
    isOpen: false,
  });
  const [toast, setToast] = useState<ToastState>({
    show: false,
    message: "",
    type: "success",
  });

  // Form state
  const [formData, setFormData] = useState({
    name: "",
    description: "",
    category: "",
    initialContent: "",
  });
  const [formLoading, setFormLoading] = useState(false);

  const showToast = useCallback((message: string, type: "success" | "error") => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: "", type: "success" }), 5000);
  }, []);

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams({
        page: page.toString(),
        limit: "20",
        sortBy,
        sortOrder,
      });

      if (search) params.append("search", search);
      if (categoryFilter) params.append("category", categoryFilter);

      const result = await api.get<PagedResult>(`/api/v1/admin/prompts?${params}`);
      if (!result) throw new Error("Unauthorized");

      setTemplates(result.templates || []);
      setTotalPages(result.totalPages || 1);
    } catch (err: any) {
      setError(err.message || "Failed to fetch templates");
      showToast("Failed to fetch templates", "error");
    } finally {
      setLoading(false);
    }
  }, [page, search, categoryFilter, sortBy, sortOrder, showToast]);

  useEffect(() => {
    fetchTemplates();
  }, [fetchTemplates]);

  useEffect(() => {
    setPage(1);
  }, [search, categoryFilter]);

  const handleSort = (column: string) => {
    if (sortBy === column) {
      setSortOrder(sortOrder === "asc" ? "desc" : "asc");
    } else {
      setSortBy(column);
      setSortOrder("asc");
    }
  };

  const openCreateModal = () => {
    setFormData({ name: "", description: "", category: "", initialContent: "" });
    setModalState({ isOpen: true, mode: "create" });
  };

  const openEditModal = (template: PromptTemplate) => {
    setFormData({
      name: template.name,
      description: template.description,
      category: template.category,
      initialContent: "",
    });
    setModalState({ isOpen: true, mode: "edit", template });
  };

  const closeModal = () => {
    setModalState({ isOpen: false, mode: "create" });
    setFormData({ name: "", description: "", category: "", initialContent: "" });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormLoading(true);

    try {
      if (modalState.mode === "create") {
        await api.post("/api/v1/admin/prompts", {
          name: formData.name,
          description: formData.description,
          category: formData.category,
          initialContent: formData.initialContent,
        });
        showToast("Template created successfully", "success");
      } else if (modalState.template) {
        await api.put(`/api/v1/admin/prompts/${modalState.template.id}`, {
          name: formData.name,
          description: formData.description,
          category: formData.category,
        });
        showToast("Template updated successfully", "success");
      }

      closeModal();
      fetchTemplates();
    } catch (err: any) {
      showToast(err.message || "Operation failed", "error");
    } finally {
      setFormLoading(false);
    }
  };

  const openDeleteDialog = (template: PromptTemplate) => {
    setDeleteDialog({ isOpen: true, template });
  };

  const closeDeleteDialog = () => {
    setDeleteDialog({ isOpen: false });
  };

  const handleDelete = async () => {
    if (!deleteDialog.template) return;

    try {
      await api.delete(`/api/v1/admin/prompts/${deleteDialog.template.id}`);
      showToast("Template deleted successfully", "success");
      closeDeleteDialog();
      fetchTemplates();
    } catch (err: any) {
      showToast(err.message || "Delete failed", "error");
    }
  };

  const navigateToDetails = (id: string) => {
    router.push(`/admin/prompts/${id}`);
  };

  return (
    <div style={{ minHeight: "100vh", background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div style={{ maxWidth: "1400px", margin: "0 auto", padding: "2rem" }}>
        <div style={{ background: "white", borderRadius: "12px", boxShadow: "0 20px 60px rgba(0,0,0,0.3)", overflow: "hidden" }}>
          {/* Header */}
          <div style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)", padding: "2rem", color: "white" }}>
            <h1 style={{ fontSize: "2rem", fontWeight: "bold", marginBottom: "0.5rem" }}>Prompt Templates</h1>
            <p style={{ opacity: 0.9 }}>Manage system prompts and AI templates</p>
          </div>

          {/* Filters */}
          <div style={{ padding: "1.5rem", borderBottom: "1px solid #e5e7eb", background: "#f9fafb" }}>
            <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap", alignItems: "center" }}>
              <input
                type="text"
                placeholder="Search by name..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                style={{ flex: "1", minWidth: "200px", padding: "0.5rem 1rem", border: "1px solid #d1d5db", borderRadius: "8px" }}
              />

              <select
                value={categoryFilter}
                onChange={(e) => setCategoryFilter(e.target.value)}
                style={{ padding: "0.5rem 1rem", border: "1px solid #d1d5db", borderRadius: "8px", minWidth: "200px" }}
              >
                <option value="">All Categories</option>
                {CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat}
                  </option>
                ))}
              </select>

              <button
                onClick={openCreateModal}
                style={{
                  padding: "0.5rem 1.5rem",
                  background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
                  color: "white",
                  border: "none",
                  borderRadius: "8px",
                  fontWeight: "600",
                  cursor: "pointer",
                  transition: "transform 0.2s",
                }}
                onMouseEnter={(e) => (e.currentTarget.style.transform = "scale(1.05)")}
                onMouseLeave={(e) => (e.currentTarget.style.transform = "scale(1)")}
              >
                Create Template
              </button>
            </div>
          </div>

          {/* Content */}
          <div style={{ padding: "1.5rem" }}>
            {loading && <div style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>Loading...</div>}

            {error && (
              <div style={{ padding: "1rem", background: "#fee2e2", color: "#991b1b", borderRadius: "8px", marginBottom: "1rem" }}>
                {error}
              </div>
            )}

            {!loading && !error && templates.length === 0 && (
              <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
                <p style={{ fontSize: "1.125rem", marginBottom: "0.5rem" }}>No templates found</p>
                <p style={{ fontSize: "0.875rem" }}>Create your first prompt template to get started</p>
              </div>
            )}

            {!loading && !error && templates.length > 0 && (
              <>
                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                      <tr style={{ background: "#f9fafb", borderBottom: "2px solid #e5e7eb" }}>
                        <th
                          onClick={() => handleSort("name")}
                          style={{ padding: "1rem", textAlign: "left", fontWeight: "600", cursor: "pointer", userSelect: "none" }}
                        >
                          Name {sortBy === "name" && (sortOrder === "asc" ? "↑" : "↓")}
                        </th>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>Description</th>
                        <th
                          onClick={() => handleSort("category")}
                          style={{ padding: "1rem", textAlign: "left", fontWeight: "600", cursor: "pointer", userSelect: "none" }}
                        >
                          Category {sortBy === "category" && (sortOrder === "asc" ? "↑" : "↓")}
                        </th>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>Active Version</th>
                        <th
                          onClick={() => handleSort("createdAt")}
                          style={{ padding: "1rem", textAlign: "left", fontWeight: "600", cursor: "pointer", userSelect: "none" }}
                        >
                          Created At {sortBy === "createdAt" && (sortOrder === "asc" ? "↑" : "↓")}
                        </th>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {templates.map((template) => (
                        <tr key={template.id} style={{ borderBottom: "1px solid #e5e7eb" }}>
                          <td style={{ padding: "1rem", fontWeight: "500" }}>{template.name}</td>
                          <td style={{ padding: "1rem", color: "#6b7280", maxWidth: "300px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                            {template.description}
                          </td>
                          <td style={{ padding: "1rem" }}>
                            <span
                              style={{
                                padding: "0.25rem 0.75rem",
                                background: "#e0e7ff",
                                color: "#4338ca",
                                borderRadius: "9999px",
                                fontSize: "0.875rem",
                                fontWeight: "500",
                              }}
                            >
                              {template.category}
                            </span>
                          </td>
                          <td style={{ padding: "1rem" }}>
                            {template.activeVersionId ? (
                              <span style={{ color: "#059669", fontWeight: "500" }}>✓ Active</span>
                            ) : (
                              <span style={{ color: "#6b7280" }}>No active version</span>
                            )}
                          </td>
                          <td style={{ padding: "1rem", color: "#6b7280" }}>
                            {new Date(template.createdAt).toLocaleDateString()}
                          </td>
                          <td style={{ padding: "1rem" }}>
                            <div style={{ display: "flex", gap: "0.5rem" }}>
                              <button
                                onClick={() => navigateToDetails(template.id)}
                                style={{
                                  padding: "0.25rem 0.75rem",
                                  background: "#e0e7ff",
                                  color: "#4338ca",
                                  border: "none",
                                  borderRadius: "6px",
                                  fontSize: "0.875rem",
                                  cursor: "pointer",
                                  fontWeight: "500",
                                }}
                              >
                                View
                              </button>
                              <button
                                onClick={() => openEditModal(template)}
                                style={{
                                  padding: "0.25rem 0.75rem",
                                  background: "#dbeafe",
                                  color: "#1e40af",
                                  border: "none",
                                  borderRadius: "6px",
                                  fontSize: "0.875rem",
                                  cursor: "pointer",
                                  fontWeight: "500",
                                }}
                              >
                                Edit
                              </button>
                              <button
                                onClick={() => openDeleteDialog(template)}
                                style={{
                                  padding: "0.25rem 0.75rem",
                                  background: "#fee2e2",
                                  color: "#991b1b",
                                  border: "none",
                                  borderRadius: "6px",
                                  fontSize: "0.875rem",
                                  cursor: "pointer",
                                  fontWeight: "500",
                                }}
                              >
                                Delete
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Pagination */}
                <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: "1rem", marginTop: "1.5rem" }}>
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                    style={{
                      padding: "0.5rem 1rem",
                      background: page === 1 ? "#e5e7eb" : "#667eea",
                      color: page === 1 ? "#9ca3af" : "white",
                      border: "none",
                      borderRadius: "8px",
                      cursor: page === 1 ? "not-allowed" : "pointer",
                      fontWeight: "500",
                    }}
                  >
                    Previous
                  </button>
                  <span style={{ color: "#6b7280", fontWeight: "500" }}>
                    Page {page} of {totalPages}
                  </span>
                  <button
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page === totalPages}
                    style={{
                      padding: "0.5rem 1rem",
                      background: page === totalPages ? "#e5e7eb" : "#667eea",
                      color: page === totalPages ? "#9ca3af" : "white",
                      border: "none",
                      borderRadius: "8px",
                      cursor: page === totalPages ? "not-allowed" : "pointer",
                      fontWeight: "500",
                    }}
                  >
                    Next
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Create/Edit Modal */}
      {modalState.isOpen && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: "rgba(0, 0, 0, 0.5)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 50,
          }}
          onClick={closeModal}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: "white",
              borderRadius: "12px",
              padding: "2rem",
              maxWidth: "800px",
              width: "90%",
              maxHeight: "90vh",
              overflowY: "auto",
            }}
          >
            <h2 style={{ fontSize: "1.5rem", fontWeight: "bold", marginBottom: "1.5rem" }}>
              {modalState.mode === "create" ? "Create Template" : "Edit Template"}
            </h2>

            <form onSubmit={handleSubmit}>
              <div style={{ marginBottom: "1rem" }}>
                <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>Name</label>
                <input
                  type="text"
                  required
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  style={{ width: "100%", padding: "0.5rem 1rem", border: "1px solid #d1d5db", borderRadius: "8px" }}
                />
              </div>

              <div style={{ marginBottom: "1rem" }}>
                <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>Description</label>
                <textarea
                  required
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  rows={3}
                  style={{ width: "100%", padding: "0.5rem 1rem", border: "1px solid #d1d5db", borderRadius: "8px" }}
                />
              </div>

              <div style={{ marginBottom: "1rem" }}>
                <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>Category</label>
                <select
                  required
                  value={formData.category}
                  onChange={(e) => setFormData({ ...formData, category: e.target.value })}
                  style={{ width: "100%", padding: "0.5rem 1rem", border: "1px solid #d1d5db", borderRadius: "8px" }}
                >
                  <option value="">Select category...</option>
                  {CATEGORIES.map((cat) => (
                    <option key={cat} value={cat}>
                      {cat}
                    </option>
                  ))}
                </select>
              </div>

              {modalState.mode === "create" && (
                <div style={{ marginBottom: "1rem" }}>
                  <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "500" }}>Initial Content</label>
                  <textarea
                    required
                    value={formData.initialContent}
                    onChange={(e) => setFormData({ ...formData, initialContent: e.target.value })}
                    rows={10}
                    placeholder="Enter the initial prompt content..."
                    style={{
                      width: "100%",
                      padding: "0.5rem 1rem",
                      border: "1px solid #d1d5db",
                      borderRadius: "8px",
                      fontFamily: "monospace",
                      fontSize: "0.875rem",
                    }}
                  />
                </div>
              )}

              <div style={{ display: "flex", gap: "1rem", justifyContent: "flex-end" }}>
                <button
                  type="button"
                  onClick={closeModal}
                  style={{
                    padding: "0.5rem 1.5rem",
                    background: "#e5e7eb",
                    color: "#374151",
                    border: "none",
                    borderRadius: "8px",
                    cursor: "pointer",
                    fontWeight: "500",
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={formLoading}
                  style={{
                    padding: "0.5rem 1.5rem",
                    background: formLoading ? "#9ca3af" : "#667eea",
                    color: "white",
                    border: "none",
                    borderRadius: "8px",
                    cursor: formLoading ? "not-allowed" : "pointer",
                    fontWeight: "500",
                  }}
                >
                  {formLoading ? "Saving..." : modalState.mode === "create" ? "Create" : "Update"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Confirmation Dialog */}
      {deleteDialog.isOpen && deleteDialog.template && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: "rgba(0, 0, 0, 0.5)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 50,
          }}
          onClick={closeDeleteDialog}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: "white",
              borderRadius: "12px",
              padding: "2rem",
              maxWidth: "500px",
              width: "90%",
            }}
          >
            <h2 style={{ fontSize: "1.5rem", fontWeight: "bold", marginBottom: "1rem", color: "#991b1b" }}>
              Confirm Delete
            </h2>
            <p style={{ marginBottom: "1.5rem", color: "#6b7280" }}>
              Are you sure you want to delete <strong>{deleteDialog.template.name}</strong>? This action cannot be undone.
            </p>
            <div style={{ display: "flex", gap: "1rem", justifyContent: "flex-end" }}>
              <button
                onClick={closeDeleteDialog}
                style={{
                  padding: "0.5rem 1.5rem",
                  background: "#e5e7eb",
                  color: "#374151",
                  border: "none",
                  borderRadius: "8px",
                  cursor: "pointer",
                  fontWeight: "500",
                }}
              >
                Cancel
              </button>
              <button
                onClick={handleDelete}
                style={{
                  padding: "0.5rem 1.5rem",
                  background: "#dc2626",
                  color: "white",
                  border: "none",
                  borderRadius: "8px",
                  cursor: "pointer",
                  fontWeight: "500",
                }}
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toast Notification */}
      {toast.show && (
        <div
          style={{
            position: "fixed",
            bottom: "2rem",
            right: "2rem",
            padding: "1rem 1.5rem",
            background: toast.type === "success" ? "#10b981" : "#ef4444",
            color: "white",
            borderRadius: "8px",
            boxShadow: "0 10px 40px rgba(0,0,0,0.3)",
            zIndex: 100,
            fontWeight: "500",
          }}
        >
          {toast.message}
        </div>
      )}
    </div>
  );
}
