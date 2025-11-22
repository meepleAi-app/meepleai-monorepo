/**
 * Admin Dashboard - Client Component
 *
 * Issue #1611 Phase 2: SSR Auth Protection Migration
 *
 * This Client Component handles all interactive logic:
 * - AI requests monitoring
 * - Analytics charts
 * - Filtering and pagination
 * - Stats dashboard
 *
 * Server Component (page.tsx) handles:
 * - Server-side authentication check
 * - Admin role authorization
 * - User prop provisioning
 */

'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import type { AuthUser } from "@/types/auth";
import { api } from "@/lib/api";
import { EndpointDistributionChart, LatencyDistributionChart, RequestsTimeSeriesChart, FeedbackChart } from "@/components/admin";
import { cn } from "@/lib/utils";

type AiRequest = {
  id: string;
  userId: string | null;
  gameId: string | null;
  endpoint: string;
  query: string | null;
  responseSnippet: string | null;
  latencyMs: number;
  tokenCount: number;
  promptTokens: number;
  completionTokens: number;
  confidence: number | null;
  status: string;
  errorMessage: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
  model: string | null;
  finishReason: string | null;
};

type Stats = {
  totalRequests: number;
  avgLatencyMs: number;
  totalTokens: number;
  successRate: number;
  endpointCounts: Record<string, number>;
  feedbackCounts: Record<string, number>;
  totalFeedback: number;
};

interface AdminClientProps {
  user: AuthUser; // Provided by Server Component (authenticated as admin)
}

export function AdminClient({ user }: AdminClientProps) {
  const [requests, setRequests] = useState<AiRequest[]>([]);
  const [stats, setStats] = useState<Stats | null>(null);
  const [filter, setFilter] = useState<string>("");
  const [endpointFilter, setEndpointFilter] = useState<string>("all");
  const [startDate, setStartDate] = useState<string>("");
  const [endDate, setEndDate] = useState<string>("");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      // Build query params
      const offset = (page - 1) * pageSize;
      const queryParams = new URLSearchParams({
        limit: pageSize.toString(),
        offset: offset.toString()
      });
      if (endpointFilter !== "all") {
        queryParams.append("endpoint", endpointFilter);
      }
      if (startDate) {
        queryParams.append("startDate", new Date(startDate).toISOString());
      }
      if (endDate) {
        queryParams.append("endDate", new Date(endDate).toISOString());
      }

      // Fetch requests
      const requestsData = await api.get<{ requests: AiRequest[]; totalCount: number }>(
        `/api/v1/admin/requests?${queryParams.toString()}`
      );

      if (!requestsData) {
        throw new Error("Unauthorized - Admin access required");
      }

      setRequests(requestsData.requests);
      setTotalCount(requestsData.totalCount);

      // Fetch stats
      const statsData = await api.get<Stats>(`/api/v1/admin/stats`);

      if (!statsData) {
        throw new Error("Unauthorized - Admin access required");
      }

      setStats({
        ...statsData,
        feedbackCounts: statsData.feedbackCounts ?? {},
        totalFeedback: statsData.totalFeedback ?? 0
      });

      setLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "An error occurred");
      setLoading(false);
    }
  }, [endpointFilter, startDate, endDate, page, pageSize]);

  // Reset to page 1 when filters change (must run before fetchData)
  useEffect(() => {
    setPage(1);
  }, [endpointFilter, startDate, endDate]);

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  const exportToCSV = () => {
    const headers = [
      "Timestamp",
      "Endpoint",
      "Query",
      "Latency (ms)",
      "Total Tokens",
      "Prompt Tokens",
      "Completion Tokens",
      "Confidence",
      "Model",
      "Finish Reason",
      "Status",
      "User ID",
      "Game ID"
    ];
    const rows = requests.map(req => [
      new Date(req.createdAt).toISOString(),
      req.endpoint,
      req.query || "",
      req.latencyMs.toString(),
      req.tokenCount.toString(),
      req.promptTokens.toString(),
      req.completionTokens.toString(),
      req.confidence !== null && req.confidence !== undefined ? req.confidence.toFixed(2) : "",
      req.model || "",
      req.finishReason || "",
      req.status,
      req.userId || "",
      req.gameId || ""
    ]);

    const csvContent = [headers, ...rows]
      .map(row => row.map(cell => `"${cell.replace(/"/g, '""')}"`).join(","))
      .join("\n");

    const blob = new Blob([csvContent], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `ai_requests_${new Date().toISOString()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const filteredRequests = (requests || []).filter(
    (req) =>
      req.query?.toLowerCase()?.includes(filter.toLowerCase()) ||
      req.endpoint.toLowerCase().includes(filter.toLowerCase()) ||
      req.userId?.toLowerCase()?.includes(filter.toLowerCase()) ||
      req.gameId?.toLowerCase()?.includes(filter.toLowerCase())
  );

  const helpfulCount = stats?.feedbackCounts?.["helpful"] ?? 0;
  const notHelpfulCount = stats?.feedbackCounts?.["not-helpful"] ?? 0;

  const getStatusColor = (status: string) => {
    return status === "Success" ? "#0f9d58" : "#d93025";
  };

  const getEndpointColor = (endpoint: string) => {
    switch (endpoint) {
      case "qa":
        return "#1a73e8";
      case "explain":
        return "#f9ab00";
      case "setup":
        return "#a142f4";
      default:
        return "#64748b"; // WCAG AA compliant (4.51:1)
    }
  };

  if (loading) {
    return (
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <h1>Loading...</h1>
      </main>
    );
  }

  if (error) {
    return (
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <h1>Error</h1>
        <p className="text-red-600">{error}</p>
        <Link href="/" className="text-blue-600">Back to Home</Link>
      </main>
    );
  }

  return (
    <main className="p-6 font-sans max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="m-0">Admin Dashboard</h1>
          <p className="mt-2 mb-0 text-gray-500">
            Monitor AI requests, latency, and token usage
          </p>
        </div>
        <div className="flex gap-3">
          <Link
            href="/admin/prompts"
            className="px-4 py-2 bg-indigo-500 text-white no-underline rounded inline-block hover:bg-indigo-600 transition-colors"
          >
            Prompt Management
          </Link>
          <Link
            href="/admin/users"
            className="px-4 py-2 bg-indigo-700 text-white no-underline rounded inline-block hover:bg-indigo-800 transition-colors"
          >
            Users
          </Link>
          <Link
            href="/admin/analytics"
            className="px-4 py-2 bg-purple-600 text-white no-underline rounded inline-block hover:bg-purple-700 transition-colors"
          >
            Analytics
          </Link>
          <Link
            href="/admin/cache"
            className="px-4 py-2 bg-emerald-600 text-white no-underline rounded inline-block hover:bg-emerald-700 transition-colors"
          >
            Cache
          </Link>
          <button
            onClick={exportToCSV}
            className="px-4 py-2 bg-green-600 text-white border-none rounded cursor-pointer hover:bg-green-700 transition-colors"
          >
            Export CSV
          </button>
          <Link
            href="/"
            className="px-4 py-2 bg-blue-600 text-white no-underline rounded inline-block hover:bg-blue-700 transition-colors"
          >
            Back to Home
          </Link>
        </div>
      </div>

      {/* Statistics Cards */}
      {stats && (
        <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-4 mb-6">
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2">Total Requests</div>
            <div className="text-3xl font-semibold">{stats.totalRequests}</div>
          </div>
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2">Avg Latency</div>
            <div className="text-3xl font-semibold">{Math.round(stats.avgLatencyMs)}ms</div>
          </div>
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2">Total Tokens</div>
            <div className="text-3xl font-semibold">{stats.totalTokens}</div>
          </div>
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2">Success Rate</div>
            <div className="text-3xl font-semibold">{(stats.successRate * 100).toFixed(1)}%</div>
          </div>
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2">Feedback Totali</div>
            <div className="text-3xl font-semibold">{stats.totalFeedback}</div>
            <div className="mt-3 flex flex-col gap-1 text-sm">
              <span className="text-green-600 font-semibold">Utile: {helpfulCount}</span>
              <span className="text-red-600 font-semibold">Non utile: {notHelpfulCount}</span>
            </div>
          </div>
        </div>
      )}

      {/* Charts Section */}
      {stats && requests.length > 0 && (
        <div className="grid grid-cols-[repeat(auto-fit,minmax(500px,1fr))] gap-4 mb-6">
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <h3 className="mt-0 mb-4">Endpoint Distribution</h3>
            <EndpointDistributionChart endpointCounts={stats.endpointCounts} />
          </div>

          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <h3 className="mt-0 mb-4">Latency Distribution</h3>
            <LatencyDistributionChart requests={filteredRequests.map(r => ({ latencyMs: r.latencyMs, endpoint: r.endpoint, createdAt: r.createdAt }))} />
          </div>

          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <h3 className="mt-0 mb-4">Requests Over Time</h3>
            <RequestsTimeSeriesChart requests={filteredRequests.map(r => ({ createdAt: r.createdAt, status: r.status }))} />
          </div>

          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <h3 className="mt-0 mb-4">User Feedback</h3>
            <FeedbackChart feedbackCounts={stats.feedbackCounts} />
          </div>
        </div>
      )}

      {/* Endpoint Breakdown */}
      {stats && (
        <div className="p-6 border border-gray-300 rounded-lg bg-white mb-6">
          <h3 className="mt-0 mb-4">Requests by Endpoint</h3>
          <div className="flex gap-6">
            {Object.entries(stats.endpointCounts).map(([endpoint, count]) => (
              <div key={endpoint} className="flex-1">
                <div className="flex items-center gap-2 mb-2">
                  <div
                    className="w-3 h-3 rounded-full"
                    style={{ background: getEndpointColor(endpoint) }}
                  />
                  <div className="text-sm font-semibold">{endpoint}</div>
                </div>
                <div className="text-2xl font-semibold" style={{ color: getEndpointColor(endpoint) }}>{count}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex gap-3 mb-4">
        <input
          type="text"
          placeholder="Filter by query, endpoint, user ID, or game ID..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="flex-1 p-3 text-sm border border-gray-300 rounded"
        />
        <select
          value={endpointFilter}
          onChange={(e) => setEndpointFilter(e.target.value)}
          className="p-3 text-sm border border-gray-300 rounded bg-white"
        >
          <option value="all">All Endpoints</option>
          <option value="qa">QA</option>
          <option value="explain">Explain</option>
          <option value="setup">Setup</option>
        </select>
      </div>

      {/* Date Range Filters */}
      <div className="flex gap-3 mb-6">
        <div className="flex-1">
          <label htmlFor="start-date-input" className="block text-xs mb-1 text-gray-500 font-semibold">
            Start Date
          </label>
          <input
            id="start-date-input"
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="w-full p-3 text-sm border border-gray-300 rounded"
          />
        </div>
        <div className="flex-1">
          <label htmlFor="end-date-input" className="block text-xs mb-1 text-gray-500 font-semibold">
            End Date
          </label>
          <input
            id="end-date-input"
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            className="w-full p-3 text-sm border border-gray-300 rounded"
          />
        </div>
        <button
          onClick={() => { setStartDate(""); setEndDate(""); }}
          disabled={!startDate && !endDate}
          className={cn(
            "px-4 text-sm border border-gray-300 rounded self-start",
            "pt-7 pb-3",
            !startDate && !endDate
              ? "bg-gray-50 text-gray-500 cursor-not-allowed"
              : "bg-gray-50 text-gray-500 cursor-pointer hover:bg-gray-100"
          )}
        >
          Clear Dates
        </button>
      </div>

      {/* Requests Table */}
      <div className="border border-gray-300 rounded-lg overflow-hidden">
        <div className="p-4 bg-gray-50 border-b border-gray-300 grid gap-4 text-xs font-semibold text-gray-500 uppercase"
          style={{ gridTemplateColumns: "140px 80px 100px 80px 90px 110px 90px 100px 160px 1fr 100px" }}
        >
          <div>Timestamp</div>
          <div>Endpoint</div>
          <div>Game ID</div>
          <div>Latency</div>
          <div>Prompt</div>
          <div>Completion</div>
          <div>Total</div>
          <div>Confidence</div>
          <div>Model</div>
          <div>Query</div>
          <div>Status</div>
        </div>

        {filteredRequests.length === 0 ? (
          <div className="p-12 text-center text-gray-500">
            <p>No AI requests found.</p>
          </div>
        ) : (
          filteredRequests.map((req) => (
            <div
              key={req.id}
              className="p-4 border-b border-gray-100 grid gap-4 text-sm items-start"
              style={{ gridTemplateColumns: "140px 80px 100px 80px 90px 110px 90px 100px 160px 1fr 100px" }}
            >
              <div className="text-gray-500 text-xs font-mono">
                {new Date(req.createdAt).toLocaleString()}
              </div>
              <div className="font-semibold text-xs" style={{ color: getEndpointColor(req.endpoint) }}>
                {req.endpoint}
              </div>
              <div className="text-xs font-mono text-gray-500">
                {req.gameId?.substring(0, 8) || "-"}
              </div>
              <div className="text-xs font-mono">{req.latencyMs}ms</div>
              <div className="text-xs font-mono text-gray-500">{req.promptTokens}</div>
              <div className="text-xs font-mono text-gray-500">{req.completionTokens}</div>
              <div className="text-xs font-mono text-gray-500">{req.tokenCount}</div>
              <div className="text-xs font-mono text-gray-500">
                {req.confidence !== null && req.confidence !== undefined ? req.confidence.toFixed(2) : "-"}
              </div>
              <div className="text-xs text-gray-500">
                {req.model ? `${req.model}${req.finishReason ? ` (${req.finishReason})` : ""}` : "-"}
              </div>
              <div className="text-xs overflow-hidden text-ellipsis whitespace-nowrap">
                {req.query || "-"}
              </div>
              <div className="font-semibold text-xs" style={{ color: getStatusColor(req.status) }}>
                {req.status}
              </div>
            </div>
          ))
        )}

        {/* Pagination Controls */}
        {totalCount > 0 && (
          <div className="p-4 border-t border-gray-300 flex justify-between items-center bg-gray-50">
            <div className="text-sm text-gray-500">
              Showing {Math.min((page - 1) * pageSize + 1, totalCount)}-{Math.min(page * pageSize, totalCount)} of {totalCount} requests
            </div>
            <div className="flex gap-2 items-center">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                className={cn(
                  "px-4 py-2 text-sm font-semibold rounded",
                  page === 1
                    ? "bg-gray-50 text-gray-500 border border-gray-300 cursor-not-allowed"
                    : "bg-blue-600 text-white border-none cursor-pointer hover:bg-blue-700"
                )}
              >
                Previous
              </button>
              <span className="text-sm text-gray-500 px-2">
                Page {page} of {Math.ceil(totalCount / pageSize)}
              </span>
              <button
                onClick={() => setPage(p => p + 1)}
                disabled={page * pageSize >= totalCount}
                className={cn(
                  "px-4 py-2 text-sm font-semibold rounded",
                  page * pageSize >= totalCount
                    ? "bg-gray-50 text-gray-500 border border-gray-300 cursor-not-allowed"
                    : "bg-blue-600 text-white border-none cursor-pointer hover:bg-blue-700"
                )}
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </main>
  );
}