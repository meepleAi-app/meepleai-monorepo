import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";

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

export default function AdminDashboard() {
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

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setPage(1);
  }, [endpointFilter, startDate, endDate]);

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

  const filteredRequests = requests.filter(
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
        return "#5f6368";
    }
  };

  if (loading) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
        <h1>Loading...</h1>
      </main>
    );
  }

  if (error) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
        <h1>Error</h1>
        <p style={{ color: "#d93025" }}>{error}</p>
        <Link href="/" style={{ color: "#1a73e8" }}>Back to Home</Link>
      </main>
    );
  }

  return (
    <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0 }}>Admin Dashboard</h1>
          <p style={{ margin: "8px 0 0 0", color: "#5f6368" }}>
            Monitor AI requests, latency, and token usage
          </p>
        </div>
        <div style={{ display: "flex", gap: 12 }}>
          <button
            onClick={exportToCSV}
            style={{
              padding: "8px 16px",
              background: "#0f9d58",
              color: "white",
              border: "none",
              borderRadius: 4,
              cursor: "pointer"
            }}
          >
            Export CSV
          </button>
          <Link
            href="/"
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              display: "inline-block"
            }}
          >
            Back to Home
          </Link>
        </div>
      </div>

      {/* Statistics Cards */}
      {stats && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 16, marginBottom: 24 }}>
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#5f6368", marginBottom: 8 }}>Total Requests</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{stats.totalRequests}</div>
          </div>
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#5f6368", marginBottom: 8 }}>Avg Latency</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{Math.round(stats.avgLatencyMs)}ms</div>
          </div>
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#5f6368", marginBottom: 8 }}>Total Tokens</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{stats.totalTokens}</div>
          </div>
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#5f6368", marginBottom: 8 }}>Success Rate</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{(stats.successRate * 100).toFixed(1)}%</div>
          </div>
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#5f6368", marginBottom: 8 }}>Feedback Totali</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{stats.totalFeedback}</div>
            <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 4, fontSize: 13 }}>
              <span style={{ color: "#34a853", fontWeight: 600 }}>Utile: {helpfulCount}</span>
              <span style={{ color: "#ea4335", fontWeight: 600 }}>Non utile: {notHelpfulCount}</span>
            </div>
          </div>
        </div>
      )}

      {/* Endpoint Breakdown */}
      {stats && (
        <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white", marginBottom: 24 }}>
          <h3 style={{ marginTop: 0, marginBottom: 16 }}>Requests by Endpoint</h3>
          <div style={{ display: "flex", gap: 24 }}>
            {Object.entries(stats.endpointCounts).map(([endpoint, count]) => (
              <div key={endpoint} style={{ flex: 1 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
                  <div
                    style={{
                      width: 12,
                      height: 12,
                      borderRadius: "50%",
                      background: getEndpointColor(endpoint)
                    }}
                  />
                  <div style={{ fontSize: 14, fontWeight: 600 }}>{endpoint}</div>
                </div>
                <div style={{ fontSize: 24, fontWeight: 600, color: getEndpointColor(endpoint) }}>{count}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Filters */}
      <div style={{ display: "flex", gap: 12, marginBottom: 16 }}>
        <input
          type="text"
          placeholder="Filter by query, endpoint, user ID, or game ID..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{
            flex: 1,
            padding: 12,
            fontSize: 14,
            border: "1px solid #dadce0",
            borderRadius: 4
          }}
        />
        <select
          value={endpointFilter}
          onChange={(e) => setEndpointFilter(e.target.value)}
          style={{
            padding: 12,
            fontSize: 14,
            border: "1px solid #dadce0",
            borderRadius: 4,
            background: "white"
          }}
        >
          <option value="all">All Endpoints</option>
          <option value="qa">QA</option>
          <option value="explain">Explain</option>
          <option value="setup">Setup</option>
        </select>
      </div>

      {/* Date Range Filters */}
      <div style={{ display: "flex", gap: 12, marginBottom: 24 }}>
        <div style={{ flex: 1 }}>
          <label style={{ display: "block", fontSize: 12, marginBottom: 4, color: "#5f6368", fontWeight: 600 }}>
            Start Date
          </label>
          <input
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            style={{
              width: "100%",
              padding: 12,
              fontSize: 14,
              border: "1px solid #dadce0",
              borderRadius: 4
            }}
          />
        </div>
        <div style={{ flex: 1 }}>
          <label style={{ display: "block", fontSize: 12, marginBottom: 4, color: "#5f6368", fontWeight: 600 }}>
            End Date
          </label>
          <input
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            style={{
              width: "100%",
              padding: 12,
              fontSize: 14,
              border: "1px solid #dadce0",
              borderRadius: 4
            }}
          />
        </div>
        <button
          onClick={() => { setStartDate(""); setEndDate(""); }}
          disabled={!startDate && !endDate}
          style={{
            padding: "28px 16px 12px 16px",
            background: !startDate && !endDate ? "#f8f9fa" : "#f8f9fa",
            color: "#5f6368",
            border: "1px solid #dadce0",
            borderRadius: 4,
            cursor: !startDate && !endDate ? "not-allowed" : "pointer",
            alignSelf: "flex-start",
            fontSize: 14
          }}
        >
          Clear Dates
        </button>
      </div>

      {/* Requests Table */}
      <div style={{ border: "1px solid #dadce0", borderRadius: 8, overflow: "hidden" }}>
        <div
          style={{
            padding: 16,
            background: "#f8f9fa",
            borderBottom: "1px solid #dadce0",
            display: "grid",
            gridTemplateColumns: "140px 80px 100px 80px 90px 110px 90px 100px 160px 1fr 100px",
            gap: 16,
            fontSize: 12,
            fontWeight: 600,
            color: "#5f6368",
            textTransform: "uppercase"
          }}
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
          <div style={{ padding: 48, textAlign: "center", color: "#5f6368" }}>
            <p>No AI requests found.</p>
          </div>
        ) : (
          filteredRequests.map((req) => (
            <div
              key={req.id}
              style={{
                padding: 16,
                borderBottom: "1px solid #f0f0f0",
                display: "grid",
                gridTemplateColumns: "140px 80px 100px 80px 90px 110px 90px 100px 160px 1fr 100px",
                gap: 16,
                fontSize: 13,
                alignItems: "start"
              }}
            >
              <div style={{ color: "#5f6368", fontSize: 11, fontFamily: "monospace" }}>
                {new Date(req.createdAt).toLocaleString()}
              </div>
              <div
                style={{
                  color: getEndpointColor(req.endpoint),
                  fontWeight: 600,
                  fontSize: 12
                }}
              >
                {req.endpoint}
              </div>
              <div style={{ fontSize: 11, fontFamily: "monospace", color: "#5f6368" }}>
                {req.gameId?.substring(0, 8) || "-"}
              </div>
              <div style={{ fontSize: 12, fontFamily: "monospace" }}>{req.latencyMs}ms</div>
              <div style={{ fontSize: 12, fontFamily: "monospace", color: "#5f6368" }}>
                {req.promptTokens}
              </div>
              <div style={{ fontSize: 12, fontFamily: "monospace", color: "#5f6368" }}>
                {req.completionTokens}
              </div>
              <div style={{ fontSize: 12, fontFamily: "monospace", color: "#5f6368" }}>
                {req.tokenCount}
              </div>
              <div style={{ fontSize: 12, fontFamily: "monospace", color: "#5f6368" }}>
                {req.confidence !== null && req.confidence !== undefined ? req.confidence.toFixed(2) : "-"}
              </div>
              <div style={{ fontSize: 12, color: "#5f6368" }}>
                {req.model ? `${req.model}${req.finishReason ? ` (${req.finishReason})` : ""}` : "-"}
              </div>
              <div style={{ fontSize: 12, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {req.query || "-"}
              </div>
              <div style={{ color: getStatusColor(req.status), fontWeight: 600, fontSize: 12 }}>
                {req.status}
              </div>
            </div>
          ))
        )}

        {/* Pagination Controls */}
        {totalCount > 0 && (
          <div style={{ padding: 16, borderTop: "1px solid #dadce0", display: "flex", justifyContent: "space-between", alignItems: "center", background: "#f8f9fa" }}>
            <div style={{ fontSize: 13, color: "#5f6368" }}>
              Showing {Math.min((page - 1) * pageSize + 1, totalCount)}-{Math.min(page * pageSize, totalCount)} of {totalCount} requests
            </div>
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                style={{
                  padding: "8px 16px",
                  background: page === 1 ? "#f8f9fa" : "#1a73e8",
                  color: page === 1 ? "#5f6368" : "white",
                  border: page === 1 ? "1px solid #dadce0" : "none",
                  borderRadius: 4,
                  cursor: page === 1 ? "not-allowed" : "pointer",
                  fontSize: 14,
                  fontWeight: 600
                }}
              >
                Previous
              </button>
              <span style={{ fontSize: 13, color: "#5f6368", padding: "0 8px" }}>
                Page {page} of {Math.ceil(totalCount / pageSize)}
              </span>
              <button
                onClick={() => setPage(p => p + 1)}
                disabled={page * pageSize >= totalCount}
                style={{
                  padding: "8px 16px",
                  background: page * pageSize >= totalCount ? "#f8f9fa" : "#1a73e8",
                  color: page * pageSize >= totalCount ? "#5f6368" : "white",
                  border: page * pageSize >= totalCount ? "1px solid #dadce0" : "none",
                  borderRadius: 4,
                  cursor: page * pageSize >= totalCount ? "not-allowed" : "pointer",
                  fontSize: 14,
                  fontWeight: 600
                }}
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
