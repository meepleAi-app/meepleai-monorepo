'use client';

import { useAuthUser } from '@/hooks/useAuthUser';

/**
 * CONFIG-06: Admin Configuration Management Page - App Router
 *
 * Provides a comprehensive UI for managing system configurations.
 * Features:
 * - Tab-based interface for different configuration categories
 * - Real-time configuration updates
 * - Feature flag management
 * - Export/import capabilities
 * - Validation and confirmation dialogs for destructive changes
 */

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { toast } from "@/components/layout";
import { api, SystemConfigurationDto } from "@/lib/api";
import FeatureFlagsTab from "@/components/admin/FeatureFlagsTab";
import CategoryConfigTab from "@/components/admin/CategoryConfigTab";
import { ErrorDisplay } from "@/components/errors";
import { categorizeError } from "@/lib/errorUtils";

// Tab types
type TabId = "feature-flags" | "rate-limiting" | "ai-llm" | "rag";

interface Tab {
  id: TabId;
  label: string;
  icon: string;
  description: string;
}

const TABS: Tab[] = [
  {
    id: "feature-flags",
    label: "Feature Flags",
    icon: "🚩",
    description: "Enable/disable features at runtime",
  },
  {
    id: "rate-limiting",
    label: "Rate Limiting",
    icon: "⚡",
    description: "Configure API rate limits per role",
  },
  {
    id: "ai-llm",
    label: "AI / LLM",
    icon: "🤖",
    description: "AI model parameters and settings",
  },
  {
    id: "rag",
    label: "RAG",
    icon: "🔍",
    description: "Vector search and chunking configuration",
  },
];

export function AdminPageClient() {
  const { user } = useAuthUser();

  if (!user) return null;
  // State
  const [activeTab, setActiveTab] = useState<TabId>("feature-flags");
  const [configurations, setConfigurations] = useState<SystemConfigurationDto[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [showBanner, setShowBanner] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Load initial data
  useEffect(() => {
    loadInitialData();
  }, []);

  const loadInitialData = async () => {
    try {
      setLoading(true);
      setError(null);

      // Load all configurations
      const result = await api.config.getConfigurations(undefined, undefined, false, 1, 100);
      setConfigurations(result.items);

      // Load categories
      const cats = await api.config.getCategories();
      setCategories(cats);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "Failed to load configurations";
      setError(errorMessage);

      if (errorMessage.includes("Unauthorized") || errorMessage.includes("403")) {
        toast.error("Admin access required");
        setTimeout(() => {
          window.location.href = "/login";
        }, 2000);
      } else {
        toast.error(errorMessage);
      }
    } finally {
      setLoading(false);
    }
  };

  const reloadConfigurations = useCallback(async () => {
    try {
      const result = await api.config.getConfigurations(undefined, undefined, false, 1, 100);
      setConfigurations(result.items);
      toast.success("Configurations reloaded");
    } catch (err) {
      toast.error("Failed to reload configurations");
    }
  }, []);

  const handleInvalidateCache = async () => {
    try {
      await api.config.invalidateCache();
      toast.success("Cache invalidated successfully");
    } catch (err) {
      toast.error("Failed to invalidate cache");
    }
  };

  // Loading state
  if (loading) {
    return (
      <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4" />
          <p className="text-slate-600 dark:text-slate-400">Loading configurations...</p>
        </div>
      </div>
    );
  }

  // Error state (if not auth error)
  if (error && !error.includes("Unauthorized") && !error.includes("403")) {
    return (
      <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="max-w-2xl w-full px-4">
          <ErrorDisplay
            error={categorizeError(new Error(error))}
            onRetry={loadInitialData}
            showTechnicalDetails={process.env.NODE_ENV === 'development'}
          />
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-dvh bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800">
        {/* Restart Reminder Banner - Sticky Top */}
        {showBanner && (
          <div className="sticky top-0 z-50 bg-yellow-50 border-b border-yellow-200 dark:bg-yellow-900/20 dark:border-yellow-800">
            <div className="max-w-7xl mx-auto px-4 py-3 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className="text-yellow-600 dark:text-yellow-400 text-xl">⚠️</span>
                <p className="text-sm text-yellow-800 dark:text-yellow-200 font-medium">
                  Configuration changes require server restart to take effect.{" "}
                  <a
                    href="https://docs.meepleai.dev/admin/restart"
                    className="underline hover:no-underline"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Learn more
                  </a>
                </p>
              </div>
              <button
                onClick={() => setShowBanner(false)}
                className="text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-200"
                aria-label="Dismiss banner"
              >
                ✕
              </button>
            </div>
          </div>
        )}

        <div className="max-w-7xl mx-auto px-4 py-8">
          {/* Header */}
          <div className="flex items-center justify-between mb-6">
            <div>
              <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
                Configuration Management
              </h1>
              <p className="text-slate-600 dark:text-slate-400">
                Manage system configurations and feature flags
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={reloadConfigurations}
                className="px-4 py-2 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-600 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
              >
                🔄 Reload
              </button>
              <button
                onClick={handleInvalidateCache}
                className="px-4 py-2 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-600 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
              >
                🗑️ Clear Cache
              </button>
              <Link
                href="/admin"
                className="px-4 py-2 bg-slate-600 text-white rounded-lg hover:bg-slate-700 transition-colors"
              >
                ← Back to Admin
              </Link>
            </div>
          </div>

          {/* Tabs */}
          <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg overflow-hidden">
            {/* Tab Headers */}
            <div className="border-b border-slate-200 dark:border-slate-700">
              <nav className="flex" aria-label="Configuration tabs">
                {TABS.map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={`flex-1 px-6 py-4 text-center font-medium transition-colors border-b-2 ${

                      activeTab === tab.id
                        ? "border-blue-600 text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/20"
                        : "border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700/50"
                    }`}
                  >
                    <div className="flex items-center justify-center gap-2">
                      <span className="text-xl">{tab.icon}</span>
                      <span>{tab.label}</span>
                    </div>
                    <p className="text-xs mt-1 opacity-75">{tab.description}</p>
                  </button>
                ))}
              </nav>
            </div>

            {/* Tab Content */}
            <div className="p-6">
              {activeTab === "feature-flags" && (
                <FeatureFlagsTab
                  configurations={configurations}
                  onConfigurationChange={reloadConfigurations}
                />
              )}

              {activeTab === "rate-limiting" && (
                <CategoryConfigTab
                  title="Rate Limiting Configuration"
                  category="RateLimiting"
                  configurations={configurations}
                  onConfigurationChange={reloadConfigurations}
                />
              )}

              {activeTab === "ai-llm" && (
                <CategoryConfigTab
                  title="AI / LLM Configuration"
                  category="AiLlm"
                  configurations={configurations}
                  onConfigurationChange={reloadConfigurations}
                />
              )}

              {activeTab === "rag" && (
                <CategoryConfigTab
                  title="RAG Configuration"
                  category="Rag"
                  configurations={configurations}
                  onConfigurationChange={reloadConfigurations}
                />
              )}
            </div>
          </div>

          {/* Footer Stats */}
          <div className="mt-6 grid grid-cols-3 gap-4">
            <div className="bg-white dark:bg-slate-800 rounded-lg p-4 shadow">
              <p className="text-sm text-slate-600 dark:text-slate-400 mb-1">Total Configurations</p>
              <p className="text-2xl font-bold text-slate-900 dark:text-white">
                {configurations.length}
              </p>
            </div>
            <div className="bg-white dark:bg-slate-800 rounded-lg p-4 shadow">
              <p className="text-sm text-slate-600 dark:text-slate-400 mb-1">Active</p>
              <p className="text-2xl font-bold text-green-600 dark:text-green-400">
                {configurations.filter((c) => c.isActive).length}
              </p>
            </div>
            <div className="bg-white dark:bg-slate-800 rounded-lg p-4 shadow">
              <p className="text-sm text-slate-600 dark:text-slate-400 mb-1">Categories</p>
              <p className="text-2xl font-bold text-blue-600 dark:text-blue-400">
                {categories.length}
              </p>
            </div>
          </div>
        </div>
      </div>
  );
}
