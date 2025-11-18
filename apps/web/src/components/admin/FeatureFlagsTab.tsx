/**
 * CONFIG-06: Feature Flags Tab Component
 *
 * Manages feature flags with toggle switches and real-time preview.
 * Features:
 * - Toggle feature flags on/off
 * - Role-based filtering
 * - Real-time active features preview
 * - Confirmation for critical flags
 */

import { useState, useEffect } from "react";
import { toast } from "@/components/layout";
import { api, SystemConfigurationDto } from "@/lib/api";
import { Switch } from "@/components/ui/switch";

interface FeatureFlagsTabProps {
  configurations: SystemConfigurationDto[];
  onConfigurationChange: () => void;
}

export default function FeatureFlagsTab({ configurations, onConfigurationChange }: FeatureFlagsTabProps) {
  const [featureFlags, setFeatureFlags] = useState<SystemConfigurationDto[]>([]);
  const [toggling, setToggling] = useState<string | null>(null);

  useEffect(() => {
    // Filter configurations to show only FeatureFlag category
    const flags = configurations.filter(
      (c) => c.category === "FeatureFlag" || c.key.startsWith("Features:")
    );
    setFeatureFlags(flags);
  }, [configurations]);

  const handleToggle = async (flag: SystemConfigurationDto) => {
    // Confirm for critical features
    const criticalFlags = ["RagCaching", "StreamingResponses", "SetupGuide"];
    const isCritical = criticalFlags.some((cf) => flag.key.includes(cf));

    if (isCritical && flag.isActive) {
      const confirmed = window.confirm(
        `Are you sure you want to disable '${flag.key}'? This may impact user experience.`
      );
      if (!confirmed) return;
    }

    setToggling(flag.id);

    try {
      const newValue = flag.value === "true" ? "false" : "true";

      await api.config.updateConfiguration(flag.id, {
        value: newValue,
      });

      toast.success(`Feature flag '${flag.key}' ${newValue === "true" ? "enabled" : "disabled"}`);
      onConfigurationChange();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "Failed to toggle feature flag";
      toast.error(errorMessage);
    } finally {
      setToggling(null);
    }
  };

  const activeFeatures = featureFlags.filter(
    (f) => f.isActive && f.value === "true"
  );

  if (featureFlags.length === 0) {
    return (
      <div className="text-center py-12">
        <p className="text-slate-600 dark:text-slate-400 text-lg mb-2">
          No feature flags found
        </p>
        <p className="text-slate-500 dark:text-slate-500 text-sm">
          Feature flags will appear here once configured in the database.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Real-time Active Features Preview */}
      <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4 border border-blue-200 dark:border-blue-800">
        <h3 className="font-semibold text-blue-900 dark:text-blue-100 mb-2 flex items-center gap-2">
          <span>✨</span>
          Currently Active Features ({activeFeatures.length})
        </h3>
        {activeFeatures.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {activeFeatures.map((feature) => (
              <span
                key={feature.id}
                className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-blue-100 dark:bg-blue-800 text-blue-800 dark:text-blue-100"
              >
                {feature.key.replace("Features:", "")}
              </span>
            ))}
          </div>
        ) : (
          <p className="text-sm text-blue-700 dark:text-blue-300">
            No features are currently enabled
          </p>
        )}
      </div>

      {/* Feature Flags Grid */}
      <div className="grid gap-4 md:grid-cols-2">
        {featureFlags.map((flag) => {
          const isEnabled = flag.value === "true" && flag.isActive;
          const isToggling = toggling === flag.id;

          return (
            <div
              key={flag.id}
              className={`bg-white dark:bg-slate-800 rounded-lg p-5 border-2 transition-all ${
                isEnabled
                  ? "border-green-500 dark:border-green-400"
                  : "border-slate-200 dark:border-slate-700"
              }`}
            >
              <div className="flex items-start justify-between mb-3">
                <div className="flex-1">
                  <h4 className="font-semibold text-slate-900 dark:text-white mb-1">
                    {flag.key.replace("Features:", "")}
                  </h4>
                  {flag.description && (
                    <p className="text-sm text-slate-600 dark:text-slate-400">
                      {flag.description}
                    </p>
                  )}
                </div>

                {/* Toggle Switch */}
                <Switch
                  checked={isEnabled}
                  onCheckedChange={() => handleToggle(flag)}
                  disabled={isToggling || !flag.isActive}
                  aria-label={`Toggle ${flag.key}`}
                  className={isEnabled ? "data-[state=checked]:bg-green-600" : ""}
                />
              </div>

              {/* Metadata */}
              <div className="flex items-center gap-3 text-xs text-slate-500 dark:text-slate-400">
                <span className={`flex items-center gap-1 ${isEnabled ? "text-green-600 dark:text-green-400 font-medium" : ""}`}>
                  {isEnabled ? "✅ Enabled" : "❌ Disabled"}
                </span>
                {flag.requiresRestart && (
                  <span className="flex items-center gap-1 text-orange-600 dark:text-orange-400">
                    ⚠️ Requires restart
                  </span>
                )}
                {!flag.isActive && (
                  <span className="flex items-center gap-1 text-red-600 dark:text-red-400">
                    🔒 Inactive
                  </span>
                )}
              </div>

              {/* Version Info */}
              {flag.version > 1 && (
                <div className="mt-2 pt-2 border-t border-slate-200 dark:border-slate-700">
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    Version {flag.version} • Updated{" "}
                    {new Date(flag.updatedAt).toLocaleDateString()}
                  </p>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Help Text */}
      <div className="bg-slate-50 dark:bg-slate-800/50 rounded-lg p-4 border border-slate-200 dark:border-slate-700">
        <h4 className="font-medium text-slate-900 dark:text-white mb-2">
          💡 Feature Flags Guide
        </h4>
        <ul className="text-sm text-slate-600 dark:text-slate-400 space-y-1">
          <li>• Toggle features on/off without code deployment</li>
          <li>• Features marked with ⚠️ require server restart to take effect</li>
          <li>• Critical features will ask for confirmation before disabling</li>
          <li>• Inactive flags (🔒) cannot be toggled until activated in the database</li>
        </ul>
      </div>
    </div>
  );
}
