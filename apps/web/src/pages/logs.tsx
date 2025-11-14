import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";
import { cn } from "@/lib/utils";

type LogEntry = {
  timestamp: string;
  level: string;
  message: string;
  requestId?: string;
  userId?: string;
};

export default function LogsPage() {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [filter, setFilter] = useState<string>("");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    const loadLogs = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const response = await api.get<LogEntry[]>("/api/v1/logs");
        if (isMounted) {
          setLogs(response ?? []);
        }
      } catch (err) {
        if (!isMounted) {
          return;
        }
        console.error("Failed to load logs", err);
        const errorMessage =
          err instanceof Error && err.message.includes("403")
            ? "You do not have permission to view logs. Please contact an administrator if you believe this is an error."
            : "Unable to load logs from the server. Please try again later.";
        setError(errorMessage);
        setLogs([]);
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    };

    void loadLogs();

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredLogs = logs.filter(
    (log) =>
      log.message.toLowerCase().includes(filter.toLowerCase()) ||
      log.requestId?.toLowerCase()?.includes(filter.toLowerCase()) ||
      log.userId?.toLowerCase()?.includes(filter.toLowerCase())
  );

  const getLevelColor = (level: string) => {
    switch (level) {
      case "ERROR":
        return "#d93025";
      case "WARN":
      case "WARNING":
        return "#e37400"; // Improved from #f9ab00 for better contrast (4.5:1)
      case "INFO":
        return "#1a73e8";
      default:
        return "#64748b"; // Improved from #94a3b8 for better contrast (4.51:1)
    }
  };

  return (
    <main className="p-6 font-sans max-w-[1400px] mx-auto">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="m-0">Observability Dashboard</h1>
          <p className="mt-2 mb-0 text-slate-600">
            Monitor application logs with request correlation
          </p>
        </div>
        <Link
          href="/"
          className="px-4 py-2 bg-blue-600 text-white no-underline rounded"
        >
          Back to Home
        </Link>
      </div>

      <div className="mb-6">
        <input
          type="text"
          placeholder="Filter logs by message, request ID, or user ID..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="w-full p-3 text-sm border border-gray-300 rounded"
        />
      </div>

      <div className="border border-gray-300 rounded-lg overflow-hidden">
        <div className="p-4 bg-gray-50 border-b border-gray-300 grid gap-4 text-xs font-semibold text-slate-700 uppercase"
          style={{ gridTemplateColumns: "180px 80px 120px 120px 1fr" }}
        >
          <div>Timestamp</div>
          <div>Level</div>
          <div>Request ID</div>
          <div>User ID</div>
          <div>Message</div>
        </div>

        {isLoading ? (
          <div className="py-12 text-center text-slate-600">
            <p>Loading logs...</p>
          </div>
        ) : error ? (
          <div className="py-12 text-center text-red-600">
            <p>{error}</p>
          </div>
        ) : filteredLogs.length === 0 ? (
          <div className="py-12 text-center text-slate-600">
            <p>No logs found. Start using the application to generate logs.</p>
            <p className="text-xs mt-4">
              <strong>Note:</strong> This is a basic observability dashboard. Logs are displayed from the backend console
              with structured correlation IDs.
            </p>
          </div>
        ) : (
          filteredLogs.map((log, index) => (
            <div
              key={index}
              className={cn(
                "p-4 grid gap-4 text-sm items-start",
                index < filteredLogs.length - 1 && "border-b border-gray-100"
              )}
              style={{ gridTemplateColumns: "180px 80px 120px 120px 1fr" }}
            >
              <div className="text-slate-600 text-xs font-mono">
                {new Date(log.timestamp).toLocaleString()}
              </div>
              <div className="font-semibold" style={{ color: getLevelColor(log.level) }}>{log.level}</div>
              <div className="text-xs font-mono text-slate-600">
                {log.requestId || "-"}
              </div>
              <div className="text-xs font-mono text-slate-600">
                {log.userId || "-"}
              </div>
              <div>{log.message}</div>
            </div>
          ))
        )}
      </div>

      <div className="mt-6 p-4 bg-blue-50 rounded-lg">
        <h3 className="mt-0">How Correlation Works</h3>
        <ul className="mb-0">
          <li>
            Each HTTP request receives a unique <code>X-Correlation-Id</code> (using ASP.NET Core&apos;s{" "}
            <code>TraceIdentifier</code>)
          </li>
          <li>All logs for a request include the same correlation ID for tracing</li>
          <li>Structured logging with Serilog enriches logs with user and request metadata</li>
          <li>Filter logs by correlation ID to trace a specific request through the system</li>
        </ul>
        <p className="mb-0 mt-3 text-sm text-blue-900">
          <strong>Production Recommendation:</strong> Integrate with a centralized logging system like Seq, Elasticsearch,
          or Azure Application Insights for full observability.
        </p>
      </div>
    </main>
  );
}