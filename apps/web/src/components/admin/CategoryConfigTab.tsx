/**
 * CONFIG-06: Category Configuration Tab Component
 *
 * Generic component for displaying and editing configurations by category.
 * Used for RateLimiting, AI/LLM, RAG categories.
 */

import { useState, useEffect } from "react";
import { toast } from "react-hot-toast";
import { api, SystemConfigurationDto, UpdateConfigurationRequest } from "../../lib/api";

interface CategoryConfigTabProps {
  title: string;
  category: string;
  configurations: SystemConfigurationDto[];
  onConfigurationChange: () => void;
}

interface EditingState {
  id: string;
  value: string;
}

export default function CategoryConfigTab({
  title,
  category,
  configurations,
  onConfigurationChange,
}: CategoryConfigTabProps) {
  const [categoryConfigs, setCategoryConfigs] = useState<SystemConfigurationDto[]>([]);
  const [editing, setEditing] = useState<EditingState | null>(null);
  const [saving, setSaving] = useState<string | null>(null);

  useEffect(() => {
    // Filter configurations by category
    const filtered = configurations.filter(
      (c) => c.category === category || c.key.startsWith(`${category}:`)
    );
    setCategoryConfigs(filtered);
  }, [configurations, category]);

  const handleEdit = (config: SystemConfigurationDto) => {
    setEditing({ id: config.id, value: config.value });
  };

  const handleCancel = () => {
    setEditing(null);
  };

  const handleSave = async (config: SystemConfigurationDto) => {
    if (!editing) return;

    // Validation
    if (!editing.value.trim()) {
      toast.error("Value cannot be empty");
      return;
    }

    // Destructive change warnings
    const destructiveKeys = ["ChunkSize", "VectorDimensions", "ChunkOverlap"];
    const isDestructive = destructiveKeys.some((key) => config.key.includes(key));

    if (isDestructive && editing.value !== config.value) {
      const confirmed = window.confirm(
        `⚠️ Warning: Changing '${config.key}' may require re-indexing vector documents. This operation may take significant time. Continue?`
      );
      if (!confirmed) {
        setEditing(null);
        return;
      }
    }

    setSaving(config.id);

    try {
      const request: UpdateConfigurationRequest = {
        value: editing.value,
      };

      await api.config.updateConfiguration(config.id, request);
      toast.success(`Configuration '${config.key}' updated successfully`);
      setEditing(null);
      onConfigurationChange();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "Failed to update configuration";
      toast.error(errorMessage);
    } finally {
      setSaving(null);
    }
  };

  const handleToggleActive = async (config: SystemConfigurationDto) => {
    setSaving(config.id);

    try {
      await api.config.updateConfiguration(config.id, {
        isActive: !config.isActive,
      });

      toast.success(
        `Configuration '${config.key}' ${config.isActive ? "deactivated" : "activated"}`
      );
      onConfigurationChange();
    } catch (err) {
      toast.error("Failed to toggle configuration");
    } finally {
      setSaving(null);
    }
  };

  if (categoryConfigs.length === 0) {
    return (
      <div className="text-center py-12">
        <p className="text-slate-600 dark:text-slate-400 text-lg mb-2">
          No {category} configurations found
        </p>
        <p className="text-slate-500 dark:text-slate-500 text-sm">
          Configurations will appear here once added to the database.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-semibold text-slate-900 dark:text-white mb-4">
        {title}
      </h2>

      {/* Configuration List */}
      <div className="space-y-3">
        {categoryConfigs.map((config) => {
          const isEditing = editing?.id === config.id;
          const isSaving = saving === config.id;

          return (
            <div
              key={config.id}
              className="bg-white dark:bg-slate-800 rounded-lg border border-slate-200 dark:border-slate-700 p-4 hover:border-blue-300 dark:hover:border-blue-600 transition-colors"
            >
              <div className="flex items-start justify-between gap-4">
                {/* Config Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-2">
                    <h3 className="font-medium text-slate-900 dark:text-white truncate">
                      {config.key}
                    </h3>
                    <span
                      className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                        config.valueType === "string"
                          ? "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200"
                          : config.valueType === "integer" || config.valueType === "int"
                          ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                          : config.valueType === "boolean" || config.valueType === "bool"
                          ? "bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200"
                          : "bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200"
                      }`}
                    >
                      {config.valueType}
                    </span>
                    {!config.isActive && (
                      <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">
                        Inactive
                      </span>
                    )}
                  </div>

                  {config.description && (
                    <p className="text-sm text-slate-600 dark:text-slate-400 mb-2">
                      {config.description}
                    </p>
                  )}

                  {/* Value Display/Edit */}
                  {isEditing ? (
                    <div className="flex items-center gap-2 mt-2">
                      <input
                        type="text"
                        value={editing.value}
                        onChange={(e) =>
                          setEditing({ ...editing, value: e.target.value })
                        }
                        className="flex-1 px-3 py-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-700 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                        disabled={isSaving}
                        autoFocus
                      />
                      <button
                        onClick={() => handleSave(config)}
                        disabled={isSaving}
                        className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-blue-400 transition-colors"
                      >
                        {isSaving ? "Saving..." : "Save"}
                      </button>
                      <button
                        onClick={handleCancel}
                        disabled={isSaving}
                        className="px-4 py-2 bg-slate-200 text-slate-700 rounded-lg hover:bg-slate-300 disabled:bg-slate-100 transition-colors"
                      >
                        Cancel
                      </button>
                    </div>
                  ) : (
                    <div className="flex items-center gap-2 mt-2">
                      <code className="flex-1 px-3 py-2 bg-slate-100 dark:bg-slate-900 text-slate-900 dark:text-white rounded-lg text-sm font-mono">
                        {config.value}
                      </code>
                      <button
                        onClick={() => handleEdit(config)}
                        disabled={isSaving || !config.isActive}
                        className="px-3 py-1.5 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-blue-400 disabled:cursor-not-allowed transition-colors"
                      >
                        Edit
                      </button>
                    </div>
                  )}

                  {/* Metadata */}
                  <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-slate-500 dark:text-slate-400">
                    <span>v{config.version}</span>
                    <span>•</span>
                    <span>Updated {new Date(config.updatedAt).toLocaleDateString()}</span>
                    {config.requiresRestart && (
                      <>
                        <span>•</span>
                        <span className="text-orange-600 dark:text-orange-400 font-medium">
                          ⚠️ Requires restart
                        </span>
                      </>
                    )}
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => handleToggleActive(config)}
                    disabled={isSaving}
                    className={`px-3 py-1.5 text-sm rounded-lg transition-colors ${
                      config.isActive
                        ? "bg-red-100 text-red-700 hover:bg-red-200 dark:bg-red-900/30 dark:text-red-300"
                        : "bg-green-100 text-green-700 hover:bg-green-200 dark:bg-green-900/30 dark:text-green-300"
                    } disabled:opacity-50 disabled:cursor-not-allowed`}
                  >
                    {config.isActive ? "Deactivate" : "Activate"}
                  </button>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Category-specific help */}
      <div className="bg-slate-50 dark:bg-slate-800/50 rounded-lg p-4 border border-slate-200 dark:border-slate-700 mt-6">
        <h4 className="font-medium text-slate-900 dark:text-white mb-2">
          💡 {category} Configuration Guide
        </h4>
        <CategoryHelpText category={category} />
      </div>
    </div>
  );
}

function CategoryHelpText({ category }: { category: string }) {
  switch (category) {
    case "RateLimiting":
      return (
        <ul className="text-sm text-slate-600 dark:text-slate-400 space-y-1">
          <li>• Configure request limits per minute for different roles</li>
          <li>• Burst size controls temporary spike allowance</li>
          <li>• Changes require server restart to take effect</li>
        </ul>
      );
    case "AiLlm":
      return (
        <ul className="text-sm text-slate-600 dark:text-slate-400 space-y-1">
          <li>• Temperature (0.0-1.0): Lower = more focused, Higher = more creative</li>
          <li>• Max Tokens (1-32000): Maximum response length</li>
          <li>• Model selection: Different models for different use cases</li>
        </ul>
      );
    case "Rag":
      return (
        <ul className="text-sm text-slate-600 dark:text-slate-400 space-y-1">
          <li>• TopK: Number of vector search results to retrieve</li>
          <li>• MinScore (0.0-1.0): Minimum similarity score threshold</li>
          <li>• ⚠️ Changing ChunkSize or VectorDimensions requires re-indexing all documents</li>
        </ul>
      );
    default:
      return (
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Modify configuration values and save changes. Check if restart is required.
        </p>
      );
  }
}
