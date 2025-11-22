'use client';


import React, { useState, useEffect, useCallback } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { ErrorDisplay } from "@/components/errors";
import { categorizeError } from "@/lib/errorUtils";
import { getErrorMessage } from '@/lib/utils/errorHandler';

type PromptAuditLog = {
  id: string;
  templateId: string;
  action: string;
  userId: string;
  userEmail: string;
  timestamp: string;
  details?: Record<string, unknown>;
};

export default function AuditLog() {
  const router = useRouter();
  const params = useParams();
  const id = params?.id as string | undefined;

  const [logs, setLogs] = useState<PromptAuditLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Handle missing ID
  if (!id) {
    return (
      <div className="min-h-screen" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
        <div className="max-w-7xl mx-auto p-8">
          <div className="bg-white rounded-xl shadow-2xl overflow-hidden p-8">
            <h1 className="text-2xl font-bold mb-4">Invalid Template ID</h1>
            <p className="text-gray-600 mb-4">No template ID provided.</p>
            <Link href="/admin/prompts">
              <button className="px-6 py-2 bg-indigo-500 text-white border-none rounded-lg cursor-pointer hover:bg-indigo-600">
                Back to Templates
              </button>
            </Link>
          </div>
        </div>
      </div>
    );
  }
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
    } catch (err) {
      setError(getErrorMessage(err, "Failed to fetch audit logs"));
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
    <div className="min-h-screen" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div className="max-w-7xl mx-auto p-8">
        <div className="bg-white rounded-xl shadow-2xl overflow-hidden">
          {/* Header */}
          <div className="p-8 text-white" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
            <div className="mb-4">
              <Link href={`/admin/prompts/${id}`}>
                <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer hover:bg-white/30">
                  ← Back to Template
                </button>
              </Link>
            </div>
            <h1 className="text-3xl font-bold mb-2">Audit Log</h1>
            <p className="opacity-90">View all changes and actions performed on this template</p>
          </div>

          {/* Filters */}
          <div className="p-6 border-b border-gray-200 bg-gray-50">
            <div className="flex gap-4 items-center">
              <label className="font-medium">Filter by action:</label>
              <select
                value={actionFilter}
                onChange={(e) => setActionFilter(e.target.value)}
                className="px-4 py-2 border border-gray-300 rounded-lg min-w-[200px]"
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
          <div className="p-6">
            {loading && <div className="text-center p-8 text-gray-500">Loading...</div>}

            {error && (
              <ErrorDisplay
                error={categorizeError(new Error(error))}
                onRetry={fetchAuditLogs}
                showTechnicalDetails={process.env.NODE_ENV === 'development'}
              />
            )}

            {!loading && logs.length === 0 && (
              <div className="text-center p-12 text-gray-500">
                <p className="text-lg mb-2">No audit logs found</p>
                <p className="text-sm">Actions performed on this template will appear here</p>
              </div>
            )}

            {!loading && logs.length > 0 && (
              <>
                <div className="overflow-x-auto">
                  <table className="w-full border-collapse">
                    <thead>
                      <tr className="bg-gray-50 border-b-2 border-gray-200">
                        <th className="p-4 text-left font-semibold">Timestamp</th>
                        <th className="p-4 text-left font-semibold">Action</th>
                        <th className="p-4 text-left font-semibold">User</th>
                        <th className="p-4 text-left font-semibold">Details</th>
                      </tr>
                    </thead>
                    <tbody>
                      {logs.map((log) => (
                        <tr key={log.id} className="border-b border-gray-200">
                          <td className="p-4 text-gray-500 text-sm">
                            {new Date(log.timestamp).toLocaleString()}
                          </td>
                          <td className="p-4">
                            <div className="flex items-center gap-2">
                              <span
                                className="inline-flex items-center justify-center w-6 h-6 rounded-full font-bold text-sm"
                                style={{
                                  background: `${getActionColor(log.action)}20`,
                                  color: getActionColor(log.action)
                                }}
                              >
                                {getActionIcon(log.action)}
                              </span>
                              <span className="font-medium capitalize">{log.action}</span>
                            </div>
                          </td>
                          <td className="p-4 text-gray-700">{log.userEmail}</td>
                          <td className="p-4 text-gray-500 text-sm">
                            {log.details && Object.keys(log.details).length > 0 ? (
                              <details>
                                <summary className="cursor-pointer text-indigo-700 font-medium">View details</summary>
                                <pre className="mt-2 p-2 bg-gray-50 rounded text-xs overflow-auto max-w-md">
                                  {JSON.stringify(log.details, null, 2)}
                                </pre>
                              </details>
                            ) : (
                              <span className="text-gray-400">No additional details</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Pagination */}
                <div className="flex justify-center items-center gap-4 mt-6">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                    className={cn(
                      "px-4 py-2 border-none rounded-lg font-medium",
                      page === 1
                        ? "bg-gray-200 text-gray-400 cursor-not-allowed"
                        : "bg-indigo-500 text-white cursor-pointer hover:bg-indigo-600"
                    )}
                  >
                    Previous
                  </button>
                  <span className="text-gray-500 font-medium">
                    Page {page} of {totalPages}
                  </span>
                  <button
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page === totalPages}
                    className={cn(
                      "px-4 py-2 border-none rounded-lg font-medium",
                      page === totalPages
                        ? "bg-gray-200 text-gray-400 cursor-not-allowed"
                        : "bg-indigo-500 text-white cursor-pointer hover:bg-indigo-600"
                    )}
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
