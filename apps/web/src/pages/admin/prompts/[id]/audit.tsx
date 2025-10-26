import React, { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "../../../../lib/api";

type PromptAuditLog = {
  id: string;
  templateId: string;
  action: string;
  userId: string;
  userEmail: string;
  timestamp: string;
  details?: Record<string, any>;
};

export default function AuditLog() {
  const router = useRouter();
  const { id } = router.query;

  const [logs, setLogs] = useState<PromptAuditLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [actionFilter, setActionFilter] = useState("");

  const fetchAuditLogs = useCallback(async () => {
    if (!id) return;

    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams({
        page: page.toString(),
        limit: "20",
      });

      if (actionFilter) params.append("action", actionFilter);

      const result = await api.get<{ logs: PromptAuditLog[]; totalPages: number }>(`/api/v1/admin/prompts/${id}/audit?${params}`);
      if (!result) throw new Error("Unauthorized");

      setLogs(result.logs || []);
      setTotalPages(result.totalPages || 1);
    } catch (err: any) {
      setError(err.message || "Failed to fetch audit logs");
    } finally {
      setLoading(false);
    }
  }, [id, page, actionFilter]);

  useEffect(() => {
    fetchAuditLogs();
  }, [fetchAuditLogs]);

  useEffect(() => {
    setPage(1);
  }, [actionFilter]);

  const getActionColor = (action: string): string => {
    if (action.includes("create")) return "#059669";
    if (action.includes("update") || action.includes("activate")) return "#2563eb";
    if (action.includes("delete")) return "#dc2626";
    return "#6b7280";
  };

  const getActionIcon = (action: string): string => {
    if (action.includes("create")) return "+";
    if (action.includes("update")) return "✎";
    if (action.includes("activate")) return "✓";
    if (action.includes("delete")) return "✕";
    return "•";
  };

  return (
    <div style={{ minHeight: "100vh", background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div style={{ maxWidth: "1400px", margin: "0 auto", padding: "2rem" }}>
        <div style={{ background: "white", borderRadius: "12px", boxShadow: "0 20px 60px rgba(0,0,0,0.3)", overflow: "hidden" }}>
          {/* Header */}
          <div style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)", padding: "2rem", color: "white" }}>
            <div style={{ marginBottom: "1rem" }}>
              <Link href={`/admin/prompts/${id}`}>
                <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                  ← Back to Template
                </button>
              </Link>
            </div>
            <h1 style={{ fontSize: "2rem", fontWeight: "bold", marginBottom: "0.5rem" }}>Audit Log</h1>
            <p style={{ opacity: 0.9 }}>View all changes and actions performed on this template</p>
          </div>

          {/* Filters */}
          <div style={{ padding: "1.5rem", borderBottom: "1px solid #e5e7eb", background: "#f9fafb" }}>
            <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
              <label style={{ fontWeight: "500" }}>Filter by action:</label>
              <select
                value={actionFilter}
                onChange={(e) => setActionFilter(e.target.value)}
                style={{ padding: "0.5rem 1rem", border: "1px solid #d1d5db", borderRadius: "8px", minWidth: "200px" }}
              >
                <option value="">All Actions</option>
                <option value="create">Create</option>
                <option value="update">Update</option>
                <option value="activate">Activate</option>
                <option value="delete">Delete</option>
              </select>
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

            {!loading && logs.length === 0 && (
              <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
                <p style={{ fontSize: "1.125rem", marginBottom: "0.5rem" }}>No audit logs found</p>
                <p style={{ fontSize: "0.875rem" }}>Actions performed on this template will appear here</p>
              </div>
            )}

            {!loading && logs.length > 0 && (
              <>
                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                      <tr style={{ background: "#f9fafb", borderBottom: "2px solid #e5e7eb" }}>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>Timestamp</th>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>Action</th>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>User</th>
                        <th style={{ padding: "1rem", textAlign: "left", fontWeight: "600" }}>Details</th>
                      </tr>
                    </thead>
                    <tbody>
                      {logs.map((log) => (
                        <tr key={log.id} style={{ borderBottom: "1px solid #e5e7eb" }}>
                          <td style={{ padding: "1rem", color: "#6b7280", fontSize: "0.875rem" }}>
                            {new Date(log.timestamp).toLocaleString()}
                          </td>
                          <td style={{ padding: "1rem" }}>
                            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                              <span
                                style={{
                                  display: "inline-flex",
                                  alignItems: "center",
                                  justifyContent: "center",
                                  width: "24px",
                                  height: "24px",
                                  borderRadius: "50%",
                                  background: `${getActionColor(log.action)}20`,
                                  color: getActionColor(log.action),
                                  fontWeight: "bold",
                                  fontSize: "0.875rem",
                                }}
                              >
                                {getActionIcon(log.action)}
                              </span>
                              <span style={{ fontWeight: "500", textTransform: "capitalize" }}>{log.action}</span>
                            </div>
                          </td>
                          <td style={{ padding: "1rem", color: "#374151" }}>{log.userEmail}</td>
                          <td style={{ padding: "1rem", color: "#6b7280", fontSize: "0.875rem" }}>
                            {log.details && Object.keys(log.details).length > 0 ? (
                              <details>
                                <summary style={{ cursor: "pointer", color: "#4338ca", fontWeight: "500" }}>View details</summary>
                                <pre
                                  style={{
                                    marginTop: "0.5rem",
                                    padding: "0.5rem",
                                    background: "#f9fafb",
                                    borderRadius: "4px",
                                    fontSize: "0.75rem",
                                    overflow: "auto",
                                    maxWidth: "400px",
                                  }}
                                >
                                  {JSON.stringify(log.details, null, 2)}
                                </pre>
                              </details>
                            ) : (
                              <span style={{ color: "#9ca3af" }}>No additional details</span>
                            )}
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
    </div>
  );
}
