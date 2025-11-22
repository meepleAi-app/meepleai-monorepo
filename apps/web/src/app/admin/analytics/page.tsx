'use client';


import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from "recharts";
import { api } from "@/lib/api";
import { ErrorDisplay } from "@/components/errors";
import { categorizeError } from "@/lib/errorUtils";
import { LoadingButton } from "@/components/loading/LoadingButton";
import { getErrorMessage } from '@/lib/utils/errorHandler';

// Types
type DashboardMetrics = {
  totalUsers: number;
  activeSessions: number;
  apiRequestsToday: number;
  totalPdfDocuments: number;
  totalChatMessages: number;
  averageConfidenceScore: number;
  totalRagRequests: number;
  totalTokensUsed: number;
};

type TimeSeriesDataPoint = {
  date: string;
  count: number;
  averageValue?: number | null;
};

type DashboardStatsDto = {
  metrics: DashboardMetrics;
  userTrend: TimeSeriesDataPoint[];
  sessionTrend: TimeSeriesDataPoint[];
  apiRequestTrend: TimeSeriesDataPoint[];
  pdfUploadTrend: TimeSeriesDataPoint[];
  chatMessageTrend: TimeSeriesDataPoint[];
  generatedAt: string;
};

type ToastMessage = {
  id: string;
  type: "success" | "error" | "info";
  message: string;
};

export default function AnalyticsDashboard() {
  // State
  const [stats, setStats] = useState<DashboardStatsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [days, setDays] = useState(30);
  const [gameId, setGameId] = useState<string>("");
  const [roleFilter, setRoleFilter] = useState<string>("all");
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(true);

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

  // Fetch dashboard stats
  const fetchStats = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const queryParams = new URLSearchParams();
      queryParams.set("days", days.toString());
      if (gameId) {
        queryParams.set("gameId", gameId);
      }
      if (roleFilter && roleFilter !== "all") {
        queryParams.set("roleFilter", roleFilter);
      }

      const data = await api.get<DashboardStatsDto>(`/api/v1/admin/analytics?${queryParams.toString()}`);

      if (!data) {
        throw new Error("Unauthorized or no data returned");
      }

      setStats(data);
      setLastUpdate(new Date());
    } catch (err) {
      const errorMessage = getErrorMessage(err, "Failed to load analytics data");
      setError(errorMessage);
      addToast("error", errorMessage);
    } finally {
      setLoading(false);
    }
  }, [days, gameId, roleFilter, addToast]);

  // Initial load
  useEffect(() => {
    fetchStats();
  }, [fetchStats]);

  // Auto-refresh every 30 seconds
  useEffect(() => {
    if (!autoRefresh) return;

    const interval = setInterval(() => {
      fetchStats();
    }, 30000); // 30 seconds

    return () => clearInterval(interval);
  }, [autoRefresh, fetchStats]);

  // Export data
  const handleExport = async (format: "csv" | "json") => {
    try {
      const requestBody = {
        format,
        fromDate: undefined,
        toDate: undefined,
        gameId: gameId || undefined,
      };

      const response = await fetch(`${process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5080"}/api/v1/admin/analytics/export`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(requestBody),
        credentials: "include",
      });

      if (!response.ok) {
        throw new Error(`Export failed: ${response.statusText}`);
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `analytics-${new Date().toISOString().split("T")[0]}.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      addToast("success", `Analytics exported as ${format.toUpperCase()}`);
    } catch (err) {
      addToast("error", getErrorMessage(err, "Export failed"));
    }
  };

  // Format numbers with commas
  const formatNumber = (num: number): string => {
    return num.toLocaleString();
  };

  // Format confidence score as percentage
  const formatConfidence = (score: number): string => {
    return `${(score * 100).toFixed(1)}%`;
  };

  // Prepare chart data
  const prepareChartData = (trend: TimeSeriesDataPoint[]) => {
    return trend.map((point) => ({
      date: new Date(point.date).toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
      }),
      count: point.count,
      average: point.averageValue || 0,
    }));
  };

  if (loading && !stats) {
    return (
      <div className="min-h-dvh bg-gray-50 p-8">
        <div className="max-w-7xl mx-auto">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-300 rounded w-1/4 mb-8"></div>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="h-32 bg-gray-300 rounded"></div>
              ))}
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (error && !stats) {
    return (
      <div className="min-h-dvh bg-gray-50 p-8">
        <div className="max-w-7xl mx-auto">
          <ErrorDisplay
            error={categorizeError(new Error(error))}
            onRetry={fetchStats}
            onDismiss={() => window.location.href = '/admin'}
            showTechnicalDetails={process.env.NODE_ENV === 'development'}
          />
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-dvh bg-gray-50 p-8">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Analytics Dashboard</h1>
            <p className="text-gray-600 mt-1">
              System metrics and usage trends
              {lastUpdate && (
                <span className="ml-2 text-sm">
                  • Last updated: {lastUpdate.toLocaleTimeString()}
                </span>
              )}
            </p>
          </div>
          <Link
            href="/admin/users"
            className="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
          >
            ← Back to Users
          </Link>
        </div>

        {/* Filters */}
        <div className="bg-white rounded-lg shadow p-6 mb-6">
          <div className="flex flex-wrap gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Time Period
              </label>
              <select
                value={days}
                onChange={(e) => setDays(Number(e.target.value))}
                className="px-3 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value={7}>Last 7 days</option>
                <option value={30}>Last 30 days</option>
                <option value={90}>Last 90 days</option>
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                User Role
              </label>
              <select
                value={roleFilter}
                onChange={(e) => setRoleFilter(e.target.value)}
                className="px-3 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="all">All Roles</option>
                <option value="Admin">Admin</option>
                <option value="Editor">Editor</option>
                <option value="User">User</option>
              </select>
            </div>

            <div className="flex items-end gap-2">
              <LoadingButton
                onClick={fetchStats}
                isLoading={loading}
                loadingText="Refreshing..."
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
              >
                Refresh
              </LoadingButton>

              <button
                onClick={() => setAutoRefresh(!autoRefresh)}
                className={`px-4 py-2 rounded ${
                  autoRefresh
                    ? "bg-green-600 text-white hover:bg-green-700"
                    : "bg-gray-300 text-gray-700 hover:bg-gray-400"
                }`}
              >
                Auto-refresh {autoRefresh ? "ON" : "OFF"}
              </button>
            </div>

            <div className="flex items-end gap-2 ml-auto">
              <button
                onClick={() => handleExport("csv")}
                className="px-4 py-2 bg-purple-600 text-white rounded hover:bg-purple-700"
              >
                Export CSV
              </button>
              <button
                onClick={() => handleExport("json")}
                className="px-4 py-2 bg-indigo-600 text-white rounded hover:bg-indigo-700"
              >
                Export JSON
              </button>
            </div>
          </div>
        </div>

        {stats && (
          <>
            {/* Metrics Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
              <MetricCard
                title="Total Users"
                value={formatNumber(stats.metrics.totalUsers)}
                icon="👥"
                bgColor="bg-blue-50"
                textColor="text-blue-700"
              />
              <MetricCard
                title="Active Sessions"
                value={formatNumber(stats.metrics.activeSessions)}
                icon="🔗"
                bgColor="bg-green-50"
                textColor="text-green-700"
              />
              <MetricCard
                title="API Requests Today"
                value={formatNumber(stats.metrics.apiRequestsToday)}
                icon="📊"
                bgColor="bg-purple-50"
                textColor="text-purple-700"
              />
              <MetricCard
                title="Total PDF Documents"
                value={formatNumber(stats.metrics.totalPdfDocuments)}
                icon="📄"
                bgColor="bg-orange-50"
                textColor="text-orange-700"
              />
              <MetricCard
                title="Total Chat Messages"
                value={formatNumber(stats.metrics.totalChatMessages)}
                icon="💬"
                bgColor="bg-pink-50"
                textColor="text-pink-700"
              />
              <MetricCard
                title="Avg Confidence Score"
                value={formatConfidence(stats.metrics.averageConfidenceScore)}
                icon="🎯"
                bgColor="bg-yellow-50"
                textColor="text-yellow-700"
              />
              <MetricCard
                title="Total RAG Requests"
                value={formatNumber(stats.metrics.totalRagRequests)}
                icon="🤖"
                bgColor="bg-indigo-50"
                textColor="text-indigo-700"
              />
              <MetricCard
                title="Total Tokens Used"
                value={formatNumber(stats.metrics.totalTokensUsed)}
                icon="🔢"
                bgColor="bg-teal-50"
                textColor="text-teal-700"
              />
            </div>

            {/* Charts */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <ChartCard title="User Registrations" data={prepareChartData(stats.userTrend)} />
              <ChartCard title="Session Creations" data={prepareChartData(stats.sessionTrend)} />
              <ChartCard
                title="API Requests"
                data={prepareChartData(stats.apiRequestTrend)}
                showAverage
              />
              <ChartCard
                title="PDF Uploads"
                data={prepareChartData(stats.pdfUploadTrend)}
                showAverage
              />
              <ChartCard title="Chat Messages" data={prepareChartData(stats.chatMessageTrend)} />
            </div>
          </>
        )}

        {/* Toast Notifications */}
        <div className="fixed top-4 right-4 z-50 space-y-2">
          {toasts.map((toast) => (
            <div
              key={toast.id}
              className={`px-6 py-3 rounded shadow-lg flex items-center justify-between min-w-[300px] ${
                toast.type === "success"
                  ? "bg-green-600 text-white"
                  : toast.type === "error"
                  ? "bg-red-600 text-white"
                  : "bg-blue-600 text-white"
              }`}
            >
              <span>{toast.message}</span>
              <button
                onClick={() => removeToast(toast.id)}
                className="ml-4 text-white hover:text-gray-200"
              >
                ×
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// Metric Card Component
function MetricCard({
  title,
  value,
  icon,
  bgColor,
  textColor,
}: {
  title: string;
  value: string;
  icon: string;
  bgColor: string;
  textColor: string;
}) {
  return (
    <div className={`${bgColor} rounded-lg shadow p-6`}>
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-600 mb-1">{title}</p>
          <p className={`text-3xl font-bold ${textColor}`}>{value}</p>
        </div>
        <div className="text-4xl">{icon}</div>
      </div>
    </div>
  );
}

// Chart Card Component
function ChartCard({
  title,
  data,
  showAverage = false,
}: {
  title: string;
  data: { date: string; count: number; average?: number }[];
  showAverage?: boolean;
}) {
  return (
    <div className="bg-white rounded-lg shadow p-6">
      <h2 className="text-xl font-semibold text-gray-900 mb-4">{title}</h2>
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="date" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <Tooltip />
          <Legend />
          <Line
            type="monotone"
            dataKey="count"
            stroke="#3b82f6"
            strokeWidth={2}
            name="Count"
            dot={{ r: 3 }}
          />
          {showAverage && (
            <Line
              type="monotone"
              dataKey="average"
              stroke="#10b981"
              strokeWidth={2}
              strokeDasharray="5 5"
              name="Average"
              dot={{ r: 2 }}
            />
          )}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
