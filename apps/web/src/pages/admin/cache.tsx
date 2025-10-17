import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api, CacheStats } from "../../lib/api";

type Game = {
  id: string;
  name: string;
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

export default function CacheDashboard() {
  const [stats, setStats] = useState<CacheStats | null>(null);
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string>("all");
  const [tagInput, setTagInput] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [confirmation, setConfirmation] = useState<ConfirmationDialog>({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: () => {}
  });

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

  // Fetch games
  const fetchGames = useCallback(async () => {
    try {
      const gamesData = await api.get<Game[]>("/api/v1/games");
      if (gamesData) {
        setGames(gamesData);
      }
    } catch (err) {
      console.error("Failed to fetch games:", err);
      // Non-critical error, don't block the page
    }
  }, []);

  // Fetch cache stats
  const fetchStats = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const gameId = selectedGameId === "all" ? undefined : selectedGameId;
      const statsData = await api.cache.getStats(gameId);

      if (!statsData) {
        throw new Error("Unauthorized - Admin access required");
      }

      setStats(statsData);
      setLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "An error occurred");
      setLoading(false);
      addToast("error", "Failed to load cache statistics");
    }
  }, [selectedGameId, addToast]);

  useEffect(() => {
    void fetchGames();
  }, [fetchGames]);

  useEffect(() => {
    void fetchStats();
  }, [fetchStats]);

  // Cache invalidation handlers
  const handleInvalidateGameCache = (gameId: string, gameName: string) => {
    setConfirmation({
      isOpen: true,
      title: "Invalidate Game Cache",
      message: `Are you sure you want to invalidate all cached responses for "${gameName}"? This action cannot be undone.`,
      onConfirm: async () => {
        try {
          await api.cache.invalidateGameCache(gameId);
          addToast("success", `Cache invalidated successfully for "${gameName}"`);
          setConfirmation({ ...confirmation, isOpen: false });
          // Refresh stats after invalidation
          await fetchStats();
        } catch (err) {
          addToast("error", `Failed to invalidate cache: ${err instanceof Error ? err.message : "Unknown error"}`);
          setConfirmation({ ...confirmation, isOpen: false });
        }
      }
    });
  };

  const handleInvalidateByTag = () => {
    if (!tagInput.trim()) {
      addToast("error", "Please enter a tag to invalidate");
      return;
    }

    setConfirmation({
      isOpen: true,
      title: "Invalidate Cache by Tag",
      message: `Are you sure you want to invalidate all cached responses with tag "${tagInput}"? This action cannot be undone.`,
      onConfirm: async () => {
        try {
          await api.cache.invalidateByTag(tagInput.trim());
          addToast("success", `Cache invalidated successfully for tag "${tagInput}"`);
          setTagInput("");
          setConfirmation({ ...confirmation, isOpen: false });
          // Refresh stats after invalidation
          await fetchStats();
        } catch (err) {
          addToast("error", `Failed to invalidate cache: ${err instanceof Error ? err.message : "Unknown error"}`);
          setConfirmation({ ...confirmation, isOpen: false });
        }
      }
    });
  };

  const handleRefresh = () => {
    addToast("info", "Refreshing cache statistics...");
    void fetchStats();
  };

  // Helper functions
  const formatCacheSize = (sizeInBytes: number): string => {
    const mb = sizeInBytes / (1024 * 1024);
    if (mb < 1) {
      return `${(sizeInBytes / 1024).toFixed(2)} KB`;
    }
    return `${mb.toFixed(2)} MB`;
  };

  const formatPercentage = (value: number): string => {
    return `${(value * 100).toFixed(1)}%`;
  };

  const formatDate = (dateString: string): string => {
    return new Date(dateString).toLocaleString();
  };

  const getHitRateColor = (hitRate: number): string => {
    if (hitRate >= 0.7) return "#0f9d58"; // Green
    if (hitRate >= 0.4) return "#f9ab00"; // Yellow
    return "#d93025"; // Red
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
        <Link href="/admin" style={{ color: "#1a73e8" }}>
          Back to Admin Dashboard
        </Link>
      </main>
    );
  }

  if (!stats) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
        <h1>Loading...</h1>
      </main>
    );
  }

  return (
    <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
      {/* Toast Notifications */}
      <div
        style={{
          position: "fixed",
          top: 24,
          right: 24,
          zIndex: 1000,
          display: "flex",
          flexDirection: "column",
          gap: 12,
          maxWidth: 400
        }}
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            role="alert"
            aria-live="polite"
            style={{
              padding: "16px 20px",
              background: toast.type === "success" ? "#0f9d58" : toast.type === "error" ? "#d93025" : "#1a73e8",
              color: "white",
              borderRadius: 8,
              boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              gap: 12
            }}
          >
            <span style={{ flex: 1 }}>{toast.message}</span>
            <button
              onClick={() => removeToast(toast.id)}
              aria-label="Close notification"
              style={{
                background: "transparent",
                border: "none",
                color: "white",
                cursor: "pointer",
                fontSize: 20,
                padding: 0,
                lineHeight: 1
              }}
            >
              ×
            </button>
          </div>
        ))}
      </div>

      {/* Confirmation Dialog */}
      {confirmation.isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="dialog-title"
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: "rgba(0,0,0,0.5)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1001
          }}
        >
          <div
            style={{
              background: "white",
              borderRadius: 8,
              padding: 24,
              maxWidth: 500,
              width: "90%",
              boxShadow: "0 8px 24px rgba(0,0,0,0.2)"
            }}
          >
            <h2 id="dialog-title" style={{ marginTop: 0, marginBottom: 16 }}>
              {confirmation.title}
            </h2>
            <p style={{ marginBottom: 24, color: "#64748b" }}>{confirmation.message}</p>
            <div style={{ display: "flex", gap: 12, justifyContent: "flex-end" }}>
              <button
                onClick={() => setConfirmation({ ...confirmation, isOpen: false })}
                style={{
                  padding: "10px 20px",
                  background: "#f8f9fa",
                  color: "#64748b",
                  border: "1px solid #dadce0",
                  borderRadius: 4,
                  cursor: "pointer",
                  fontSize: 14,
                  fontWeight: 600
                }}
              >
                Cancel
              </button>
              <button
                onClick={confirmation.onConfirm}
                style={{
                  padding: "10px 20px",
                  background: "#d93025",
                  color: "white",
                  border: "none",
                  borderRadius: 4,
                  cursor: "pointer",
                  fontSize: 14,
                  fontWeight: 600
                }}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0 }}>Cache Management Dashboard</h1>
          <p style={{ margin: "8px 0 0 0", color: "#64748b" }}>
            Monitor cache performance and manage cached responses
          </p>
        </div>
        <div style={{ display: "flex", gap: 12 }}>
          <button
            onClick={handleRefresh}
            aria-label="Refresh cache statistics"
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              border: "none",
              borderRadius: 4,
              cursor: "pointer",
              fontSize: 14,
              fontWeight: 600
            }}
          >
            Refresh
          </button>
          <Link
            href="/admin"
            style={{
              padding: "8px 16px",
              background: "#64748b",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              display: "inline-block",
              fontSize: 14,
              fontWeight: 600
            }}
          >
            Back to Admin
          </Link>
        </div>
      </div>

      {/* Game Filter */}
      <div style={{ marginBottom: 24 }}>
        <label htmlFor="game-filter" style={{ display: "block", fontSize: 14, marginBottom: 8, fontWeight: 600 }}>
          Filter by Game
        </label>
        <select
          id="game-filter"
          value={selectedGameId}
          onChange={(e) => setSelectedGameId(e.target.value)}
          style={{
            padding: 12,
            fontSize: 14,
            border: "1px solid #dadce0",
            borderRadius: 4,
            background: "white",
            minWidth: 300
          }}
        >
          <option value="all">All Games</option>
          {games.map((game) => (
            <option key={game.id} value={game.id}>
              {game.name}
            </option>
          ))}
        </select>
      </div>

      {/* Statistics Cards */}
      {stats && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))", gap: 16, marginBottom: 24 }}>
          {/* Cache Hit Rate */}
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#64748b", marginBottom: 8, fontWeight: 600 }}>Cache Hit Rate</div>
            <div style={{ fontSize: 32, fontWeight: 600, color: getHitRateColor(stats.hitRate), marginBottom: 12 }}>
              {formatPercentage(stats.hitRate)}
            </div>
            <div style={{ background: "#f0f0f0", height: 8, borderRadius: 4, overflow: "hidden" }}>
              <div
                role="progressbar"
                aria-valuenow={stats.hitRate * 100}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label="Cache hit rate"
                style={{
                  width: `${stats.hitRate * 100}%`,
                  height: "100%",
                  background: getHitRateColor(stats.hitRate),
                  transition: "width 0.3s ease"
                }}
              />
            </div>
            <div style={{ marginTop: 8, fontSize: 12, color: "#64748b" }}>
              Miss Rate: {formatPercentage(stats.missRate)}
            </div>
          </div>

          {/* Total Requests */}
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#64748b", marginBottom: 8, fontWeight: 600 }}>Total Requests</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{stats.totalRequests.toLocaleString()}</div>
            <div style={{ marginTop: 12, fontSize: 13 }}>
              <div style={{ color: "#0f9d58", fontWeight: 600 }}>
                Cached: {Math.round(stats.totalRequests * stats.hitRate).toLocaleString()}
              </div>
              <div style={{ color: "#d93025", fontWeight: 600 }}>
                Not Cached: {Math.round(stats.totalRequests * stats.missRate).toLocaleString()}
              </div>
            </div>
          </div>

          {/* Cache Size */}
          <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
            <div style={{ fontSize: 12, color: "#64748b", marginBottom: 8, fontWeight: 600 }}>Cache Size</div>
            <div style={{ fontSize: 32, fontWeight: 600 }}>{formatCacheSize(stats.cacheSize)}</div>
            <div style={{ marginTop: 12, fontSize: 13, color: "#64748b" }}>
              {stats.topQuestions.length} cached questions
            </div>
          </div>
        </div>
      )}

      {/* Cache Management Actions */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(400px, 1fr))", gap: 16, marginBottom: 24 }}>
        {/* Invalidate by Game */}
        <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
          <h3 style={{ marginTop: 0, marginBottom: 16 }}>Invalidate Game Cache</h3>
          <p style={{ fontSize: 14, color: "#64748b", marginBottom: 16 }}>
            Clear all cached responses for a specific game
          </p>
          {selectedGameId !== "all" ? (
            <button
              onClick={() => {
                const game = games.find((g) => g.id === selectedGameId);
                if (game) {
                  handleInvalidateGameCache(game.id, game.name);
                }
              }}
              style={{
                padding: "10px 20px",
                background: "#d93025",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: "pointer",
                fontSize: 14,
                fontWeight: 600,
                width: "100%"
              }}
            >
              Invalidate Cache for {games.find((g) => g.id === selectedGameId)?.name}
            </button>
          ) : (
            <p style={{ fontSize: 14, color: "#64748b", fontStyle: "italic" }}>
              Select a specific game to invalidate its cache
            </p>
          )}
        </div>

        {/* Invalidate by Tag */}
        <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
          <h3 style={{ marginTop: 0, marginBottom: 16 }}>Invalidate by Tag</h3>
          <p style={{ fontSize: 14, color: "#64748b", marginBottom: 16 }}>
            Clear cached responses associated with a specific tag
          </p>
          <div style={{ display: "flex", gap: 8 }}>
            <input
              type="text"
              placeholder="Enter tag (e.g., qa, setup)"
              value={tagInput}
              onChange={(e) => setTagInput(e.target.value)}
              onKeyPress={(e) => {
                if (e.key === "Enter") {
                  handleInvalidateByTag();
                }
              }}
              aria-label="Tag to invalidate"
              style={{
                flex: 1,
                padding: "10px 12px",
                fontSize: 14,
                border: "1px solid #dadce0",
                borderRadius: 4
              }}
            />
            <button
              onClick={handleInvalidateByTag}
              disabled={!tagInput.trim()}
              aria-label="Invalidate cache by tag"
              style={{
                padding: "10px 20px",
                background: !tagInput.trim() ? "#f8f9fa" : "#d93025",
                color: !tagInput.trim() ? "#64748b" : "white",
                border: !tagInput.trim() ? "1px solid #dadce0" : "none",
                borderRadius: 4,
                cursor: !tagInput.trim() ? "not-allowed" : "pointer",
                fontSize: 14,
                fontWeight: 600
              }}
            >
              Invalidate
            </button>
          </div>
        </div>
      </div>

      {/* Top Questions Table */}
      {stats && stats.topQuestions.length > 0 && (
        <div style={{ border: "1px solid #dadce0", borderRadius: 8, overflow: "hidden" }}>
          <div
            style={{
              padding: 16,
              background: "#f8f9fa",
              borderBottom: "1px solid #dadce0"
            }}
          >
            <h3 style={{ margin: 0 }}>Top Cached Questions</h3>
            <p style={{ margin: "8px 0 0 0", fontSize: 14, color: "#64748b" }}>
              Most frequently accessed questions from the cache
            </p>
          </div>
          <div
            style={{
              padding: 16,
              background: "#f8f9fa",
              borderBottom: "1px solid #dadce0",
              display: "grid",
              gridTemplateColumns: "1fr 120px 200px",
              gap: 16,
              fontSize: 12,
              fontWeight: 600,
              color: "#64748b",
              textTransform: "uppercase"
            }}
          >
            <div>Question</div>
            <div>Hit Count</div>
            <div>Last Hit</div>
          </div>

          {stats.topQuestions.map((question, index) => (
            <div
              key={`${question.question}-${index}`}
              style={{
                padding: 16,
                borderBottom: index < stats.topQuestions.length - 1 ? "1px solid #f0f0f0" : "none",
                display: "grid",
                gridTemplateColumns: "1fr 120px 200px",
                gap: 16,
                fontSize: 14,
                alignItems: "start"
              }}
            >
              <div style={{ overflow: "hidden", textOverflow: "ellipsis" }}>
                {question.question}
              </div>
              <div style={{ fontWeight: 600, color: "#1a73e8" }}>
                {question.hitCount}
              </div>
              <div style={{ fontSize: 12, color: "#64748b", fontFamily: "monospace" }}>
                {formatDate(question.lastHitAt)}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Empty State */}
      {stats && stats.topQuestions.length === 0 && (
        <div style={{ padding: 48, textAlign: "center", border: "1px solid #dadce0", borderRadius: 8, background: "white" }}>
          <p style={{ fontSize: 16, color: "#64748b", margin: 0 }}>
            No cached questions found. Cache will populate as users interact with the system.
          </p>
        </div>
      )}
    </main>
  );
}
