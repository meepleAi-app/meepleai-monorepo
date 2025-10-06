import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";

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
        const response = await api.get<LogEntry[]>("/logs");
        if (isMounted) {
          setLogs(response ?? []);
        }
      } catch (err) {
        if (!isMounted) {
          return;
        }
        console.error("Failed to load logs", err);
        setError("Unable to load logs from the server. Please try again later.");
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
        return "#f9ab00";
      case "INFO":
        return "#1a73e8";
      default:
        return "#5f6368";
    }
  };

  return (
    <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0 }}>Observability Dashboard</h1>
          <p style={{ margin: "8px 0 0 0", color: "#5f6368" }}>
            Monitor application logs with request correlation
          </p>
        </div>
        <Link
          href="/"
          style={{
            padding: "8px 16px",
            background: "#1a73e8",
            color: "white",
            textDecoration: "none",
            borderRadius: 4
          }}
        >
          Back to Home
        </Link>
      </div>

      <div style={{ marginBottom: 24 }}>
        <input
          type="text"
          placeholder="Filter logs by message, request ID, or user ID..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{
            width: "100%",
            padding: 12,
            fontSize: 14,
            border: "1px solid #dadce0",
            borderRadius: 4
          }}
        />
      </div>

      <div style={{ border: "1px solid #dadce0", borderRadius: 8, overflow: "hidden" }}>
        <div
          style={{
            padding: 16,
            background: "#f8f9fa",
            borderBottom: "1px solid #dadce0",
            display: "grid",
            gridTemplateColumns: "180px 80px 120px 120px 1fr",
            gap: 16,
            fontSize: 12,
            fontWeight: 600,
            color: "#5f6368",
            textTransform: "uppercase"
          }}
        >
          <div>Timestamp</div>
          <div>Level</div>
          <div>Request ID</div>
          <div>User ID</div>
          <div>Message</div>
        </div>

        {isLoading ? (
          <div style={{ padding: 48, textAlign: "center", color: "#5f6368" }}>
            <p>Loading logs...</p>
          </div>
        ) : error ? (
          <div style={{ padding: 48, textAlign: "center", color: "#d93025" }}>
            <p>{error}</p>
          </div>
        ) : filteredLogs.length === 0 ? (
          <div style={{ padding: 48, textAlign: "center", color: "#5f6368" }}>
            <p>No logs found. Start using the application to generate logs.</p>
            <p style={{ fontSize: 12, marginTop: 16 }}>
              <strong>Note:</strong> This is a basic observability dashboard. Logs are displayed from the backend console
              with structured correlation IDs.
            </p>
          </div>
        ) : (
          filteredLogs.map((log, index) => (
            <div
              key={index}
              style={{
                padding: 16,
                borderBottom: index < filteredLogs.length - 1 ? "1px solid #f0f0f0" : "none",
                display: "grid",
                gridTemplateColumns: "180px 80px 120px 120px 1fr",
                gap: 16,
                fontSize: 13,
                alignItems: "start"
              }}
            >
              <div style={{ color: "#5f6368", fontSize: 12, fontFamily: "monospace" }}>
                {new Date(log.timestamp).toLocaleString()}
              </div>
              <div style={{ color: getLevelColor(log.level), fontWeight: 600 }}>{log.level}</div>
              <div style={{ fontSize: 11, fontFamily: "monospace", color: "#5f6368" }}>
                {log.requestId || "-"}
              </div>
              <div style={{ fontSize: 11, fontFamily: "monospace", color: "#5f6368" }}>
                {log.userId || "-"}
              </div>
              <div>{log.message}</div>
            </div>
          ))
        )}
      </div>

      <div style={{ marginTop: 24, padding: 16, background: "#e8f0fe", borderRadius: 8 }}>
        <h3 style={{ marginTop: 0 }}>How Correlation Works</h3>
        <ul style={{ marginBottom: 0 }}>
          <li>
            Each HTTP request receives a unique <code>X-Correlation-Id</code> (using ASP.NET Core&apos;s{" "}
            <code>TraceIdentifier</code>)
          </li>
          <li>All logs for a request include the same correlation ID for tracing</li>
          <li>Structured logging with Serilog enriches logs with user and request metadata</li>
          <li>Filter logs by correlation ID to trace a specific request through the system</li>
        </ul>
        <p style={{ marginBottom: 0, marginTop: 12, fontSize: 13, color: "#5f6368" }}>
          <strong>Production Recommendation:</strong> Integrate with a centralized logging system like Seq, Elasticsearch,
          or Azure Application Insights for full observability.
        </p>
      </div>
    </main>
  );
}