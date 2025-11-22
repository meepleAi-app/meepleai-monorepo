'use client';

import type { AuthUser } from '@/types/auth';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { getErrorMessage } from '@/lib/utils/errorHandler';

type AuthResponse = {
  user: AuthUser;
  expiresAt: string;
};

type Game = {
  id: string;
  title: string;
  description?: string | null;
  createdAt: string;
};

export function AdminPageClient() {
  const router = useRouter();
  const { user: authUser } = useAuthUser();
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameIds, setSelectedGameIds] = useState<Set<string>>(new Set());
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isExporting, setIsExporting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string>("");

  if (!authUser) return null;

  // Check if user is Editor or Admin
  if (authUser.role !== "Editor" && authUser.role !== "Admin") {
    setErrorMessage("Access denied. Editor or Admin role required.");
  }

  useEffect(() => {
    void loadGames();
  }, []);

  const loadGames = async () => {
    setIsLoading(true);
    setErrorMessage("");
    try {
      const gamesData = await api.get<Game[]>("/api/v1/games");
      if (gamesData) {
        setGames(gamesData);
      }
    } catch (err) {
      setErrorMessage(getErrorMessage(err, "Failed to load games."));
    } finally {
      setIsLoading(false);
    }
  };

  const toggleGameSelection = (gameId: string) => {
    const newSelection = new Set(selectedGameIds);
    if (newSelection.has(gameId)) {
      newSelection.delete(gameId);
    } else {
      newSelection.add(gameId);
    }
    setSelectedGameIds(newSelection);
  };

  const toggleSelectAll = () => {
    if (selectedGameIds.size === games.length) {
      setSelectedGameIds(new Set());
    } else {
      setSelectedGameIds(new Set(games.map(g => g.id)));
    }
  };

  const handleExport = async () => {
    if (selectedGameIds.size === 0) {
      setErrorMessage("Please select at least one game to export.");
      return;
    }

    setIsExporting(true);
    setErrorMessage("");
    setStatusMessage("");

    try {
      await (api.chat as any).bulkExportRuleSpecs({ ruleSpecIds: Array.from(selectedGameIds) });
      setStatusMessage(`Successfully exported ${selectedGameIds.size} rule spec(s).`);
      setSelectedGameIds(new Set()); // Clear selection
    } catch (err) {
      setErrorMessage(getErrorMessage(err, "Export failed."));
    } finally {
      setIsExporting(false);
    }
  };

  if (!authUser) {
    return (
      <div className="min-h-dvh bg-slate-950 text-white flex items-center justify-center">
        <p>Loading...</p>
      </div>
    );
  }

  if (authUser.role !== "Editor" && authUser.role !== "Admin") {
    return (
      <div className="min-h-dvh bg-slate-950 text-white flex items-center justify-center">
        <div className="text-center">
          <h1 className="text-2xl font-bold mb-4">Access Denied</h1>
          <p className="text-slate-400 mb-6">Editor or Admin role required.</p>
          <Link href="/" className="text-blue-400 hover:underline">
            Return to Home
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-dvh bg-slate-950 text-white">
      {/* Header */}
      <header className="sticky top-0 glass z-50">
        <div className="max-w-7xl mx-auto px-6 py-4 flex justify-between items-center">
          <Link href="/" className="flex items-center gap-3 hover:opacity-80 transition-opacity">
            <span className="text-4xl">🎲</span>
            <span className="text-2xl font-bold gradient-text">MeepleAI</span>
          </Link>
          <nav className="flex items-center gap-6">
            <Link href="/admin" className="text-slate-300 hover:text-white transition-colors">
              Admin
            </Link>
            <Link href="/chat" className="text-slate-300 hover:text-white transition-colors">
              Chat
            </Link>
          </nav>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-6 py-12">
        <div className="mb-8">
          <h1 className="text-4xl font-bold mb-2">Bulk Export Rule Specs</h1>
          <p className="text-slate-400">Select games to export their rule specs as a ZIP file.</p>
        </div>

        {/* Status Messages */}
        {errorMessage && (
          <div className="mb-6 p-4 bg-red-900/20 border border-red-500 rounded-lg text-red-200">
            {errorMessage}
          </div>
        )}

        {statusMessage && (
          <div className="mb-6 p-4 bg-green-900/20 border border-green-500 rounded-lg text-green-200">
            {statusMessage}
          </div>
        )}

        {/* Bulk Actions */}
        <div className="mb-6 flex items-center justify-between p-4 bg-slate-800/50 rounded-lg border border-slate-700">
          <div className="flex items-center gap-4">
            <label className="flex items-center gap-2 text-slate-300 cursor-pointer hover:text-white">
              <input
                type="checkbox"
                checked={games.length > 0 && selectedGameIds.size === games.length}
                onChange={toggleSelectAll}
                className="w-4 h-4 rounded border-slate-600 bg-slate-700 text-blue-500 focus:ring-2 focus:ring-blue-500"
              />
              <span>Select All</span>
            </label>
            <span className="text-slate-400">
              {selectedGameIds.size} of {games.length} selected
            </span>
          </div>

          <Button
            onClick={handleExport}
            disabled={selectedGameIds.size === 0 || isExporting}
            className="px-6 py-2"
            aria-label={`Export ${selectedGameIds.size} selected rule specs`}
          >
            {isExporting ? "Exporting..." : `Export ${selectedGameIds.size > 0 ? selectedGameIds.size : ""} Rule Spec${selectedGameIds.size !== 1 ? "s" : ""}`}
          </Button>
        </div>

        {/* Games List */}
        {isLoading ? (
          <div className="text-center py-12">
            <p className="text-slate-400">Loading games...</p>
          </div>
        ) : games.length === 0 ? (
          <div className="text-center py-12">
            <p className="text-slate-400">No games found.</p>
            <Link href="/upload" className="text-blue-400 hover:underline mt-4 inline-block">
              Upload a PDF to create your first rule spec
            </Link>
          </div>
        ) : (
          <div className="space-y-2">
            {games.map((game) => (
              <label
                key={game.id}
                className="flex items-center gap-4 p-4 bg-slate-800/30 hover:bg-slate-800/50 rounded-lg border border-slate-700 cursor-pointer transition-colors"
              >
                <input
                  type="checkbox"
                  checked={selectedGameIds.has(game.id)}
                  onChange={() => toggleGameSelection(game.id)}
                  className="w-4 h-4 rounded border-slate-600 bg-slate-700 text-blue-500 focus:ring-2 focus:ring-blue-500"
                  aria-label={`Select ${game.title}`}
                />
                <div className="flex-1">
                  <h3 className="font-semibold text-white">{game.title}</h3>
                  {game.description && (
                    <p className="text-sm text-slate-400 mt-1">{game.description}</p>
                  )}
                  <p className="text-xs text-slate-500 mt-1">
                    Game ID: {game.id} • Created: {new Date(game.createdAt).toLocaleDateString()}
                  </p>
                </div>
                <Link
                  href={`/editor?gameId=${encodeURIComponent(game.id)}`}
                  className="text-blue-400 hover:underline text-sm"
                  onClick={(e) => e.stopPropagation()}
                >
                  Edit
                </Link>
                <Link
                  href={`/versions?gameId=${encodeURIComponent(game.id)}`}
                  className="text-blue-400 hover:underline text-sm"
                  onClick={(e) => e.stopPropagation()}
                >
                  Versions
                </Link>
              </label>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
