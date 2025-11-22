'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api, CacheStats } from "@/lib/api";
import { cn } from "@/lib/utils";
import { ErrorDisplay } from "@/components/errors";
import { categorizeError } from "@/lib/errorUtils";
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

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
      logger.error(
        'Failed to fetch games for cache dashboard',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CacheDashboard', 'fetchGames', { operation: 'load_games' })
      );
      // Non-critical error, don't block the page
    }
  }, []);

  // Fetch cache stats
  const fetchStats = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const gameId = selectedGameId === "all" ? undefined : selectedGameId;
      const statsData = await api.chat.getCacheStats(gameId);

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
          await api.chat.invalidateGameCache(gameId);
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
          await api.chat.invalidateCacheByTag(tagInput.trim());
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
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <h1>Loading...</h1>
      </main>
    );
  }

  if (error) {
    return (
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <div className="mb-4">
          <ErrorDisplay
            error={categorizeError(new Error(error))}
            onRetry={fetchStats}
            onDismiss={() => window.location.href = '/admin'}
            showTechnicalDetails={process.env.NODE_ENV === 'development'}
          />
        </div>
        <Link href="/admin" className="text-blue-600">
          Back to Admin Dashboard
        </Link>
      </main>
    );
  }

  if (!stats) {
    return (
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <h1>Loading...</h1>
      </main>
    );
  }

  return (
    <main className="p-6 font-sans max-w-7xl mx-auto">
      {/* Toast Notifications */}
      <div className="fixed top-6 right-6 z-[1000] flex flex-col gap-3 max-w-md">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            role="alert"
            aria-live="polite"
            className={cn(
              "px-5 py-4 text-white rounded-lg shadow-lg flex justify-between items-center gap-3",
              toast.type === "success" && "bg-green-600",
              toast.type === "error" && "bg-red-600",
              toast.type === "info" && "bg-blue-600"
            )}
          >
            <span className="flex-1">{toast.message}</span>
            <button
              onClick={() => removeToast(toast.id)}
              aria-label="Close notification"
              className="bg-transparent border-none text-white cursor-pointer text-xl p-0 leading-none"
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
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-[1001]"
        >
          <div className="bg-white rounded-lg p-6 max-w-lg w-[90%] shadow-2xl">
            <h2 id="dialog-title" className="mt-0 mb-4">
              {confirmation.title}
            </h2>
            <p className="mb-6 text-gray-500">{confirmation.message}</p>
            <div className="flex gap-3 justify-end">
              <button
                onClick={() => setConfirmation({ ...confirmation, isOpen: false })}
                className="px-5 py-2 bg-gray-50 text-gray-500 border border-gray-300 rounded cursor-pointer text-sm font-semibold hover:bg-gray-100"
              >
                Cancel
              </button>
              <button
                onClick={confirmation.onConfirm}
                className="px-5 py-2 bg-red-600 text-white border-none rounded cursor-pointer text-sm font-semibold hover:bg-red-700"
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="m-0">Cache Management Dashboard</h1>
          <p className="mt-2 mb-0 text-gray-500">
            Monitor cache performance and manage cached responses
          </p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={handleRefresh}
            aria-label="Refresh cache statistics"
            className="px-4 py-2 bg-blue-600 text-white border-none rounded cursor-pointer text-sm font-semibold hover:bg-blue-700"
          >
            Refresh
          </button>
          <Link
            href="/admin"
            className="px-4 py-2 bg-gray-500 text-white no-underline rounded inline-block text-sm font-semibold hover:bg-gray-600"
          >
            Back to Admin
          </Link>
        </div>
      </div>

      {/* Game Filter */}
      <div className="mb-6">
        <label htmlFor="game-filter" className="block text-sm mb-2 font-semibold">
          Filter by Game
        </label>
        <select
          id="game-filter"
          value={selectedGameId}
          onChange={(e) => setSelectedGameId(e.target.value)}
          className="p-3 text-sm border border-gray-300 rounded bg-white min-w-[300px]"
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
        <div className="grid grid-cols-[repeat(auto-fit,minmax(250px,1fr))] gap-4 mb-6">
          {/* Cache Hit Rate */}
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2 font-semibold">Cache Hit Rate</div>
            <div className="text-3xl font-semibold mb-3" style={{ color: getHitRateColor(stats.hitRate ?? 0) }}>
              {formatPercentage(stats.hitRate ?? 0)}
            </div>
            <div className="bg-gray-200 h-2 rounded overflow-hidden">
              <div
                role="progressbar"
                aria-valuenow={(stats.hitRate ?? 0) * 100}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label="Cache hit rate"
                className="h-full transition-all duration-300"
                style={{
                  width: `${(stats.hitRate ?? 0) * 100}%`,
                  background: getHitRateColor(stats.hitRate ?? 0)
                }}
              />
            </div>
            <div className="mt-2 text-xs text-gray-500">
              Miss Rate: {formatPercentage(1 - (stats.hitRate ?? 0))}
            </div>
          </div>

          {/* Total Requests */}
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2 font-semibold">Total Requests</div>
            <div className="text-3xl font-semibold">
              {((stats.totalHits ?? 0) + (stats.totalMisses ?? 0)).toLocaleString()}
            </div>
            <div className="mt-3 text-sm">
              <div className="text-green-600 font-semibold">
                Cached: {(stats.totalHits ?? 0).toLocaleString()}
              </div>
              <div className="text-red-600 font-semibold">
                Not Cached: {(stats.totalMisses ?? 0).toLocaleString()}
              </div>
            </div>
          </div>

          {/* Cache Size */}
          <div className="p-6 border border-gray-300 rounded-lg bg-white">
            <div className="text-xs text-gray-500 mb-2 font-semibold">Cache Size</div>
            <div className="text-3xl font-semibold">{formatCacheSize(stats.cacheSizeBytes ?? 0)}</div>
            <div className="mt-3 text-sm text-gray-500">
              {stats.totalKeys ?? 0} cached keys
            </div>
          </div>
        </div>
      )}

      {/* Cache Management Actions */}
      <div className="grid grid-cols-[repeat(auto-fit,minmax(400px,1fr))] gap-4 mb-6">
        {/* Invalidate by Game */}
        <div className="p-6 border border-gray-300 rounded-lg bg-white">
          <h3 className="mt-0 mb-4">Invalidate Game Cache</h3>
          <p className="text-sm text-gray-500 mb-4">
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
              className="w-full px-5 py-2 bg-red-600 text-white border-none rounded cursor-pointer text-sm font-semibold hover:bg-red-700"
            >
              Invalidate Cache for {games.find((g) => g.id === selectedGameId)?.name}
            </button>
          ) : (
            <p className="text-sm text-gray-500 italic">
              Select a specific game to invalidate its cache
            </p>
          )}
        </div>

        {/* Invalidate by Tag */}
        <div className="p-6 border border-gray-300 rounded-lg bg-white">
          <h3 className="mt-0 mb-4">Invalidate by Tag</h3>
          <p className="text-sm text-gray-500 mb-4">
            Clear cached responses associated with a specific tag
          </p>
          <div className="flex gap-2">
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
              className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded"
            />
            <button
              onClick={handleInvalidateByTag}
              disabled={!tagInput.trim()}
              aria-label="Invalidate cache by tag"
              className={cn(
                "px-5 py-2 text-sm font-semibold rounded",
                !tagInput.trim()
                  ? "bg-gray-50 text-gray-500 border border-gray-300 cursor-not-allowed"
                  : "bg-red-600 text-white border-none cursor-pointer hover:bg-red-700"
              )}
            >
              Invalidate
            </button>
          </div>
        </div>
      </div>

      {/* Top Questions Table */}
      {stats && stats.topQuestions.length > 0 && (
        <div className="border border-gray-300 rounded-lg overflow-hidden">
          <div className="p-4 bg-gray-50 border-b border-gray-300">
            <h3 className="m-0">Top Cached Questions</h3>
            <p className="mt-2 mb-0 text-sm text-gray-500">
              Most frequently accessed questions from the cache
            </p>
          </div>
          <div className="p-4 bg-gray-50 border-b border-gray-300 grid gap-4 text-xs font-semibold text-gray-500 uppercase"
            style={{ gridTemplateColumns: "1fr 120px 200px" }}
          >
            <div>Question Hash</div>
            <div>Hit Count</div>
            <div>Last Hit</div>
          </div>

          {stats.topQuestions.map((question, index) => (
            <div
              key={`${question.questionHash}-${index}`}
              className={cn(
                "p-4 grid gap-4 text-sm items-start",
                index < stats.topQuestions.length - 1 && "border-b border-gray-100"
              )}
              style={{ gridTemplateColumns: "1fr 120px 200px" }}
            >
              <div className="overflow-hidden text-ellipsis">
                {question.questionHash}
              </div>
              <div className="font-semibold text-blue-600">
                {question.hitCount}
              </div>
              <div className="text-xs text-gray-500 font-mono">
                {question.lastHitAt ? formatDate(question.lastHitAt) : 'N/A'}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Empty State */}
      {stats && stats.topQuestions.length === 0 && (
        <div className="p-12 text-center border border-gray-300 rounded-lg bg-white">
          <p className="text-base text-gray-500 m-0">
            No cached questions found. Cache will populate as users interact with the system.
          </p>
        </div>
      )}
    </main>
  );
}