/**
 * CONFIG-06: Feature Flags Tab Component (Issue #3079)
 *
 * Manages feature flags with toggle switches and real-time preview.
 * Features:
 * - Toggle feature flags on/off (role-based)
 * - Tier-based feature flags (Free/Normal/Premium) - Issue #3079
 * - Role-based filtering
 * - Real-time active features preview
 * - Confirmation for critical flags
 * - Bulk actions for tier management
 * - Visual differentiation between role and tier toggles
 */

import { useState, useEffect, useMemo, useCallback } from 'react';

import { Check, Clock, Crown, ScrollText, Users, Zap } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import { Switch } from '@/components/ui/forms/switch';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import { BulkActionBar, type BulkAction } from './BulkActionBar';
import { ConfigAuditLogDialog } from './ConfigAuditLogDialog';
import { ConfigHistoryDialog } from './ConfigHistoryDialog';
import { DirtyStateBar } from './DirtyStateBar';
import { EnvPill } from './EnvPill';
import {
  detectFlagCategory,
  FlagCategoryTabs,
  readCategoryFromHash,
  writeCategoryToHash,
  type FlagCategory,
} from './FlagCategoryTabs';
import {
  api,
  SystemConfigurationDto,
  TIER_DESCRIPTIONS,
  TIER_ORDER,
  type SubscriptionTier,
} from '../../lib/api';

interface FeatureFlagsTabProps {
  configurations: SystemConfigurationDto[];
  onConfigurationChange: () => void;
}

interface PendingChange {
  newValue: string;
  originalValue: string;
}

const CRITICAL_FLAG_KEYWORDS = ['RagCaching', 'StreamingResponses', 'SetupGuide'];

function isCriticalFlag(key: string): boolean {
  return CRITICAL_FLAG_KEYWORDS.some(cf => key.includes(cf));
}

type TierField = 'tierFree' | 'tierNormal' | 'tierPremium';

const TIER_FIELD_MAP: Record<SubscriptionTier, TierField> = {
  Free: 'tierFree',
  Normal: 'tierNormal',
  Premium: 'tierPremium',
};

const TIER_ICONS: Record<SubscriptionTier, React.ReactNode> = {
  Free: <Users className="h-4 w-4" />,
  Normal: <Zap className="h-4 w-4" />,
  Premium: <Crown className="h-4 w-4" />,
};

const TIER_COLORS: Record<SubscriptionTier, string> = {
  Free: 'text-muted-foreground',
  Normal: 'text-blue-600 dark:text-blue-400',
  Premium: 'text-amber-600 dark:text-amber-400',
};

export default function FeatureFlagsTab({
  configurations,
  onConfigurationChange,
}: FeatureFlagsTabProps) {
  const [featureFlags, setFeatureFlags] = useState<SystemConfigurationDto[]>([]);
  const [toggling, setToggling] = useState<string | null>(null);
  const [selectedFlags, setSelectedFlags] = useState<Set<string>>(new Set());
  const [historyConfigId, setHistoryConfigId] = useState<string | null>(null);
  const [historyConfigKey, setHistoryConfigKey] = useState('');
  // Issue #1836: global audit-log dialog (aggregates history across feature flags).
  const [auditLogOpen, setAuditLogOpen] = useState(false);
  // Issue #1836: Batch-save (dirty state) for global toggles.
  // Tier toggles still apply immediately; only the role-based toggle is staged.
  const [pendingChanges, setPendingChanges] = useState<Map<string, PendingChange>>(new Map());
  const [applying, setApplying] = useState(false);
  // Issue #1836: sub-tab category filter (URL-hash-backed).
  const [activeCategory, setActiveCategory] = useState<FlagCategory>('all');

  useEffect(() => {
    // Filter configurations to show only FeatureFlag category
    const flags = configurations.filter(
      c => c.category === 'FeatureFlag' || c.key.startsWith('Features:')
    );
    setFeatureFlags(flags);
    // Clear selection + pending changes when configurations change (server refetch).
    setSelectedFlags(new Set());
    setPendingChanges(new Map());
  }, [configurations]);

  // Issue #1836: Warn before navigating away with unsaved changes.
  useEffect(() => {
    if (pendingChanges.size === 0) return;

    const handler = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      // Some browsers still require returnValue to be set.
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [pendingChanges.size]);

  // Issue #1836: hydrate active category from URL hash on mount, and follow
  // back/forward navigation via hashchange.
  useEffect(() => {
    setActiveCategory(readCategoryFromHash());
    const handler = () => setActiveCategory(readCategoryFromHash());
    window.addEventListener('hashchange', handler);
    return () => window.removeEventListener('hashchange', handler);
  }, []);

  const handleCategoryChange = useCallback((next: FlagCategory) => {
    setActiveCategory(next);
    writeCategoryToHash(next);
  }, []);

  const visibleFlags = useMemo(() => {
    if (activeCategory === 'all') return featureFlags;
    return featureFlags.filter(f => detectFlagCategory(f.key) === activeCategory);
  }, [featureFlags, activeCategory]);

  const flagKeys = useMemo(() => featureFlags.map(f => f.key), [featureFlags]);

  const hasTierSupport = useMemo(() => {
    return featureFlags.some(
      f => f.tierFree !== undefined || f.tierNormal !== undefined || f.tierPremium !== undefined
    );
  }, [featureFlags]);

  /**
   * Issue #1836: Stage a toggle as a pending change instead of persisting it
   * immediately. The mutation lands on the backend only when the admin clicks
   * "Apply" in the {@link DirtyStateBar}. If the new value matches the original
   * server value the entry is removed (no-op).
   */
  const handleToggle = useCallback(
    (flag: SystemConfigurationDto) => {
      if (!flag.isActive) return;

      const currentValue = pendingChanges.get(flag.id)?.newValue ?? flag.value;
      const newValue = currentValue === 'true' ? 'false' : 'true';

      setPendingChanges(prev => {
        const next = new Map(prev);
        const existing = next.get(flag.id);
        const originalValue = existing?.originalValue ?? flag.value;

        if (newValue === originalValue) {
          // Reverted to server state — drop the entry.
          next.delete(flag.id);
        } else {
          next.set(flag.id, { newValue, originalValue });
        }

        return next;
      });
    },
    [pendingChanges]
  );

  const revertPendingChanges = useCallback(() => {
    setPendingChanges(new Map());
  }, []);

  const applyPendingChanges = useCallback(async () => {
    if (pendingChanges.size === 0) return;

    // Batch confirm if any critical flag is being disabled.
    const criticalDisables = Array.from(pendingChanges.entries())
      .filter(([flagId, change]) => {
        if (change.newValue !== 'false') return false;
        const flag = featureFlags.find(f => f.id === flagId);
        return flag ? isCriticalFlag(flag.key) : false;
      })
      .map(([flagId]) => featureFlags.find(f => f.id === flagId)?.key ?? flagId);

    if (criticalDisables.length > 0) {
      const message =
        criticalDisables.length === 1
          ? `You are about to disable '${criticalDisables[0]}'. This may impact user experience. Continue?`
          : `You are about to disable ${criticalDisables.length} critical flags:\n${criticalDisables
              .map(k => `• ${k}`)
              .join('\n')}\n\nContinue?`;
      const confirmed = window.confirm(message);
      if (!confirmed) return;
    }

    setApplying(true);
    const failures = new Map<string, PendingChange>();
    let successCount = 0;

    for (const [flagId, change] of pendingChanges.entries()) {
      try {
        await api.config.updateConfiguration(flagId, { value: change.newValue });
        successCount++;
      } catch {
        failures.set(flagId, change);
      }
    }

    if (successCount > 0) {
      const flagWord = successCount === 1 ? 'flag' : 'flags';
      toast.success(`Applied ${successCount} feature ${flagWord}`);
    }

    if (failures.size > 0) {
      const flagWord = failures.size === 1 ? 'flag' : 'flags';
      toast.error(`Failed to update ${failures.size} ${flagWord} — see audit log.`);
    }

    setPendingChanges(failures);
    setApplying(false);

    if (successCount > 0) {
      onConfigurationChange();
    }
  }, [pendingChanges, featureFlags, onConfigurationChange]);

  const handleTierToggle = async (flag: SystemConfigurationDto, tier: SubscriptionTier) => {
    // Issue #3335: Tier-based feature access toggle
    setToggling(`${flag.id}-${tier}`);
    const tierField = TIER_FIELD_MAP[tier];
    const currentValue = flag[tierField] ?? false;
    const newValue = !currentValue;

    try {
      // Use the feature key format expected by the backend
      const featureKey = flag.key;

      if (newValue) {
        await api.config.enableFeatureForTier(featureKey, tier.toLowerCase());
      } else {
        await api.config.disableFeatureForTier(featureKey, tier.toLowerCase());
      }

      toast.success(
        `${tier} tier ${newValue ? 'enabled' : 'disabled'} for '${flag.key.replace('Features:', '')}'`
      );
      onConfigurationChange();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to update tier access';
      toast.error(errorMessage);
    } finally {
      setToggling(null);
    }
  };

  const handleBulkTierAction = useCallback(
    async (tier: SubscriptionTier, enable: boolean) => {
      if (selectedFlags.size === 0) {
        toast.error('No feature flags selected');
        return;
      }

      // Issue #3335: Bulk tier-based feature access updates
      const selectedFlagsList = featureFlags.filter(f => selectedFlags.has(f.id));
      let successCount = 0;
      let errorCount = 0;

      for (const flag of selectedFlagsList) {
        try {
          if (enable) {
            await api.config.enableFeatureForTier(flag.key, tier.toLowerCase());
          } else {
            await api.config.disableFeatureForTier(flag.key, tier.toLowerCase());
          }
          successCount++;
        } catch {
          errorCount++;
        }
      }

      if (successCount > 0) {
        toast.success(
          `${tier} tier ${enable ? 'enabled' : 'disabled'} for ${successCount} flag(s)`
        );
        onConfigurationChange();
      }

      if (errorCount > 0) {
        toast.error(`Failed to update ${errorCount} flag(s)`);
      }

      // Clear selection after bulk action
      setSelectedFlags(new Set());
    },
    [selectedFlags, featureFlags, onConfigurationChange]
  );

  const toggleFlagSelection = useCallback((flagId: string) => {
    setSelectedFlags(prev => {
      const newSet = new Set(prev);
      if (newSet.has(flagId)) {
        newSet.delete(flagId);
      } else {
        newSet.add(flagId);
      }
      return newSet;
    });
  }, []);

  const selectAllFlags = useCallback(() => {
    setSelectedFlags(new Set(featureFlags.map(f => f.id)));
  }, [featureFlags]);

  const clearSelection = useCallback(() => {
    setSelectedFlags(new Set());
  }, []);

  const activeFeatures = featureFlags.filter(f => f.isActive && f.value === 'true');

  const bulkActions: BulkAction[] = useMemo(
    () => [
      {
        id: 'enable-premium',
        label: 'Enable Premium',
        icon: Crown,
        variant: 'default' as const,
        onClick: () => handleBulkTierAction('Premium', true),
        tooltip: 'Enable Premium tier for selected flags',
      },
      {
        id: 'enable-normal',
        label: 'Enable Normal',
        icon: Zap,
        variant: 'outline' as const,
        onClick: () => handleBulkTierAction('Normal', true),
        tooltip: 'Enable Normal tier for selected flags',
      },
      {
        id: 'disable-free',
        label: 'Disable Free',
        icon: Users,
        variant: 'secondary' as const,
        onClick: () => handleBulkTierAction('Free', false),
        tooltip: 'Disable Free tier for selected flags',
      },
    ],
    [handleBulkTierAction]
  );

  if (featureFlags.length === 0) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground text-lg mb-2">No feature flags found</p>
        <p className="text-muted-foreground/70 text-sm">
          Feature flags will appear here once configured in the database.
        </p>
      </div>
    );
  }

  return (
    <TooltipProvider>
      <div className="space-y-6">
        {/* Bulk Actions Bar */}
        {hasTierSupport && (
          <BulkActionBar
            selectedCount={selectedFlags.size}
            totalCount={featureFlags.length}
            actions={bulkActions}
            onClearSelection={clearSelection}
            itemLabel="flags"
            testId="feature-flags-bulk-actions"
          />
        )}

        {/* Real-time Active Features Preview */}
        <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4 border border-blue-200 dark:border-blue-800">
          <h3 className="font-semibold text-blue-900 dark:text-blue-100 mb-2 flex items-center gap-2">
            <span>✨</span>
            Currently Active Features ({activeFeatures.length})
          </h3>
          {activeFeatures.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {activeFeatures.map(feature => (
                <span
                  key={feature.id}
                  className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-blue-100 dark:bg-blue-800 text-blue-800 dark:text-blue-100"
                >
                  {feature.key.replace('Features:', '')}
                </span>
              ))}
            </div>
          ) : (
            <p className="text-sm text-blue-700 dark:text-blue-300">
              No features are currently enabled
            </p>
          )}
        </div>

        {/* Select All / Clear Selection (when tier support is available) */}
        {hasTierSupport && (
          <div className="flex items-center gap-2">
            <button
              onClick={selectAllFlags}
              className="text-sm text-primary hover:underline"
              type="button"
            >
              Select all
            </button>
            <span className="text-muted-foreground">•</span>
            <button
              onClick={clearSelection}
              className="text-sm text-muted-foreground hover:text-foreground"
              type="button"
            >
              Clear selection
            </button>
          </div>
        )}

        {/* Issue #1836: sub-tab category filter + global audit log entry-point */}
        <div className="flex items-end justify-between gap-3 flex-wrap">
          <FlagCategoryTabs
            flagKeys={flagKeys}
            activeCategory={activeCategory}
            onCategoryChange={handleCategoryChange}
            className="flex-1 min-w-0"
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setAuditLogOpen(true)}
            disabled={featureFlags.length === 0}
            data-testid="btn-open-audit-log"
            className="shrink-0"
          >
            <ScrollText className="h-4 w-4 mr-1.5" aria-hidden="true" />
            Audit log
          </Button>
        </div>

        {/* Feature Flags Table/Grid */}
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-border/50">
                {hasTierSupport && (
                  <th className="px-4 py-3 text-left w-10">
                    <input
                      type="checkbox"
                      checked={
                        selectedFlags.size === featureFlags.length && featureFlags.length > 0
                      }
                      onChange={e => (e.target.checked ? selectAllFlags() : clearSelection())}
                      className="rounded border-border"
                      aria-label="Select all feature flags"
                    />
                  </th>
                )}
                <th className="px-4 py-3 text-left font-semibold text-foreground">Feature</th>
                <th className="px-4 py-3 text-center font-semibold text-foreground">
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <span className="cursor-help">Enabled</span>
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>Global feature toggle (role-based)</p>
                    </TooltipContent>
                  </Tooltip>
                </th>
                {/* Tier Columns */}
                {hasTierSupport &&
                  TIER_ORDER.map(tier => (
                    <th key={tier} className="px-4 py-3 text-center font-semibold">
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <div
                            className={cn(
                              'inline-flex items-center gap-1.5 cursor-help',

                              TIER_COLORS[tier]
                            )}
                          >
                            {TIER_ICONS[tier]}
                            <span>{tier}</span>
                          </div>
                        </TooltipTrigger>
                        <TooltipContent>
                          <p>{TIER_DESCRIPTIONS[tier]}</p>
                        </TooltipContent>
                      </Tooltip>
                    </th>
                  ))}
                <th className="px-4 py-3 text-left font-semibold text-foreground">Status</th>
                <th className="px-4 py-3 text-center font-semibold text-foreground">History</th>
              </tr>
            </thead>
            <tbody>
              {visibleFlags.length === 0 && (
                <tr>
                  <td
                    colSpan={hasTierSupport ? 7 : 4}
                    className="px-4 py-12 text-center text-sm text-muted-foreground"
                    data-testid="flag-category-empty"
                  >
                    No feature flags match this category.
                  </td>
                </tr>
              )}
              {visibleFlags.map(flag => {
                const pending = pendingChanges.get(flag.id);
                const displayValue = pending?.newValue ?? flag.value;
                const isEnabled = displayValue === 'true' && flag.isActive;
                const isDirty = pending !== undefined;
                const isSelected = selectedFlags.has(flag.id);

                return (
                  <tr
                    key={flag.id}
                    data-dirty={isDirty || undefined}
                    className={cn(
                      'border-b border-border/30 transition-colors',
                      isSelected && 'bg-primary/5',
                      isEnabled && !isDirty && 'bg-green-50/50 dark:bg-green-900/10',
                      isDirty &&
                        'bg-[hsl(var(--c-warning)/0.08)] ring-1 ring-inset ring-[hsl(var(--c-warning)/0.25)]'
                    )}
                  >
                    {/* Selection Checkbox */}
                    {hasTierSupport && (
                      <td className="px-4 py-4">
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => toggleFlagSelection(flag.id)}
                          className="rounded border-border"
                          aria-label={`Select ${flag.key}`}
                        />
                      </td>
                    )}

                    {/* Feature Name & Description */}
                    <td className="px-4 py-4">
                      <div className="flex flex-col gap-1">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-medium text-foreground">
                            {flag.key.replace('Features:', '')}
                          </span>
                          {/* Issue #1836: env-pill is a static placeholder until the
                              backend exposes a per-config environment scope. */}
                          <EnvPill env="prd" compact />
                        </div>
                        {flag.description && (
                          <span className="text-sm text-muted-foreground">{flag.description}</span>
                        )}
                      </div>
                    </td>

                    {/* Global Toggle */}
                    <td className="px-4 py-4 text-center">
                      <Switch
                        checked={isEnabled}
                        onCheckedChange={() => handleToggle(flag)}
                        disabled={applying || !flag.isActive}
                        aria-label={`Toggle ${flag.key}`}
                        className={isEnabled ? 'data-[state=checked]:bg-green-600' : ''}
                      />
                    </td>

                    {/* Tier Toggles */}
                    {hasTierSupport &&
                      TIER_ORDER.map(tier => {
                        const tierField = TIER_FIELD_MAP[tier];

                        const tierValue = flag[tierField];
                        const isTierEnabled = tierValue === true;
                        const isTierToggling = toggling === `${flag.id}-${tier}`;
                        const hasTierConfig = tierValue !== undefined;

                        return (
                          <td key={tier} className="px-4 py-4 text-center">
                            {hasTierConfig ? (
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <div className="inline-flex">
                                    <Switch
                                      checked={isTierEnabled}
                                      onCheckedChange={() => handleTierToggle(flag, tier)}
                                      disabled={isTierToggling || !flag.isActive || !isEnabled}
                                      aria-label={`Toggle ${tier} tier for ${flag.key}`}
                                      className={cn(
                                        'scale-90',
                                        isTierEnabled &&
                                          tier === 'Premium' &&
                                          'data-[state=checked]:bg-amber-500',
                                        isTierEnabled &&
                                          tier === 'Normal' &&
                                          'data-[state=checked]:bg-blue-500',
                                        isTierEnabled &&
                                          tier === 'Free' &&
                                          'data-[state=checked]:bg-muted-foreground'
                                      )}
                                    />
                                  </div>
                                </TooltipTrigger>
                                <TooltipContent>
                                  <p>
                                    {isTierEnabled ? 'Enabled' : 'Disabled'} for {tier} tier
                                  </p>
                                </TooltipContent>
                              </Tooltip>
                            ) : (
                              <Badge variant="outline" className="text-xs">
                                N/A
                              </Badge>
                            )}
                          </td>
                        );
                      })}

                    {/* Status Badges */}
                    <td className="px-4 py-4">
                      <div className="flex items-center gap-2 flex-wrap">
                        <Badge variant={isEnabled ? 'default' : 'secondary'}>
                          {isEnabled ? (
                            <>
                              <Check className="h-3 w-3 mr-1" />
                              Enabled
                            </>
                          ) : (
                            'Disabled'
                          )}
                        </Badge>
                        {isDirty && (
                          <Badge
                            variant="outline"
                            className="text-[hsl(var(--c-warning))] border-[hsl(var(--c-warning)/0.5)]"
                            data-testid={`flag-${flag.id}-dirty-badge`}
                          >
                            Modified
                          </Badge>
                        )}
                        {flag.requiresRestart && (
                          <Badge variant="outline" className="text-orange-600 border-orange-300">
                            ⚠️ Restart
                          </Badge>
                        )}
                        {!flag.isActive && <Badge variant="destructive">🔒 Inactive</Badge>}
                        {flag.version > 1 && (
                          <Badge variant="outline" className="text-xs">
                            v{flag.version}
                          </Badge>
                        )}
                      </div>
                    </td>

                    {/* History Button */}
                    <td className="px-4 py-4 text-center">
                      <button
                        type="button"
                        onClick={() => {
                          setHistoryConfigId(flag.id);
                          setHistoryConfigKey(flag.key);
                        }}
                        className="inline-flex items-center justify-center rounded-md p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
                        aria-label={`History for ${flag.key}`}
                        data-testid={`btn-history-${flag.id}`}
                      >
                        <Clock className="h-4 w-4" />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* Help Text */}
        <div className="bg-muted/50 dark:bg-card rounded-lg p-4 border border-border/50 dark:border-border/30">
          <h4 className="font-medium text-foreground mb-2">💡 Feature Flags Guide</h4>
          <ul className="text-sm text-muted-foreground space-y-1">
            <li>• Toggle features on/off without code deployment</li>
            <li>• Features marked with ⚠️ require server restart to take effect</li>
            <li>• Critical features will ask for confirmation before disabling</li>
            <li>• Inactive flags (🔒) cannot be toggled until activated in the database</li>
            {hasTierSupport && (
              <>
                <li>
                  • <Crown className="inline h-3 w-3 text-amber-500" /> Premium: Full access to all
                  features
                </li>
                <li>
                  • <Zap className="inline h-3 w-3 text-blue-500" /> Normal: Standard subscription
                  features
                </li>
                <li>
                  • <Users className="inline h-3 w-3 text-muted-foreground" /> Free: Basic access
                  with limitations
                </li>
              </>
            )}
          </ul>
        </div>
      </div>

      {/* History Dialog (per-flag) */}
      <ConfigHistoryDialog
        open={!!historyConfigId}
        onOpenChange={open => {
          if (!open) setHistoryConfigId(null);
        }}
        configId={historyConfigId ?? ''}
        configKey={historyConfigKey}
        onRollbackComplete={onConfigurationChange}
      />

      {/* Issue #1836: aggregate audit log across all feature flags */}
      <ConfigAuditLogDialog
        open={auditLogOpen}
        onOpenChange={setAuditLogOpen}
        configurationIds={flagKeys.length > 0 ? featureFlags.map(f => f.id) : []}
      />

      {/* Issue #1836: batch-save dirty state bar */}
      <DirtyStateBar
        dirtyCount={pendingChanges.size}
        onRevert={revertPendingChanges}
        onApply={applyPendingChanges}
        applying={applying}
        itemLabel={{ singular: 'flag', plural: 'flags' }}
        testId="feature-flags-dirty-bar"
      />
    </TooltipProvider>
  );
}
