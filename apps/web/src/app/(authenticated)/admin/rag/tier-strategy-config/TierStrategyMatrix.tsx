'use client';

/**
 * TierStrategyMatrix Component
 * Issue #3440: Admin UI for tier-strategy configuration
 *
 * Displays and allows editing of the tier-strategy access matrix.
 */

import { useMemo } from 'react';

import { Crown, Users, Zap, ShieldCheck, UserX } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import { Switch } from '@/components/ui/forms/switch';
import { useUpdateTierStrategyAccess } from '@/hooks/queries/useTierStrategy';
import type { TierStrategyMatrixDto } from '@/lib/api';
import { cn } from '@/lib/utils';

interface TierStrategyMatrixProps {
  matrix: TierStrategyMatrixDto;
}

const TIER_ICONS: Record<string, React.ReactNode> = {
  Anonymous: <UserX className="h-4 w-4" />,
  User: <Users className="h-4 w-4" />,
  Editor: <Zap className="h-4 w-4" />,
  Admin: <Crown className="h-4 w-4" />,
};

const TIER_COLORS: Record<string, string> = {
  Anonymous: 'text-red-600 dark:text-red-400',
  User: 'text-slate-600 dark:text-slate-400',
  Editor: 'text-blue-600 dark:text-blue-400',
  Admin: 'text-amber-600 dark:text-amber-400',
};

const TIER_DESCRIPTIONS: Record<string, string> = {
  Anonymous: 'Unauthenticated users (no access)',
  User: 'Standard registered users',
  Editor: 'Content editors with advanced access',
  Admin: 'Full administrative access',
};

const STRATEGY_COLORS: Record<string, string> = {
  FAST: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300',
  BALANCED: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
  PRECISE: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300',
  EXPERT: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-300',
  CONSENSUS: 'bg-pink-100 text-pink-800 dark:bg-pink-900/30 dark:text-pink-300',
  CUSTOM: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
};

export function TierStrategyMatrix({ matrix }: TierStrategyMatrixProps) {
  const updateMutation = useUpdateTierStrategyAccess();

  const accessMap = useMemo(() => {
    const map = new Map<string, boolean>();
    matrix.accessMatrix.forEach(entry => {
      map.set(`${entry.tier}-${entry.strategy}`, entry.isEnabled);
    });
    return map;
  }, [matrix.accessMatrix]);

  const defaultMap = useMemo(() => {
    const map = new Map<string, boolean>();
    matrix.accessMatrix.forEach(entry => {
      map.set(`${entry.tier}-${entry.strategy}`, entry.isDefault);
    });
    return map;
  }, [matrix.accessMatrix]);

  const handleToggle = async (tier: string, strategy: string, currentValue: boolean) => {
    try {
      await updateMutation.mutateAsync({
        tier,
        strategy,
        isEnabled: !currentValue,
      });
      toast.success(`${!currentValue ? 'Enabled' : 'Disabled'} ${strategy} for ${tier} tier`);
    } catch (_error) {
      toast.error('Failed to update access');
    }
  };

  return (
    <TooltipProvider>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="border-b border-border/50">
              <th className="px-4 py-3 text-left font-semibold text-foreground">Tier</th>
              {matrix.strategies.map(strategy => (
                <th key={strategy.name} className="px-4 py-3 text-center">
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <div className="cursor-help">
                        <Badge className={cn('font-medium', STRATEGY_COLORS[strategy.name] || '')}>
                          {strategy.displayName}
                        </Badge>
                        {strategy.requiresAdmin && (
                          <ShieldCheck className="h-3 w-3 ml-1 inline text-amber-500" />
                        )}
                      </div>
                    </TooltipTrigger>
                    <TooltipContent side="top" className="max-w-xs">
                      <p className="font-semibold">{strategy.displayName}</p>
                      <p className="text-sm text-muted-foreground">{strategy.description}</p>
                      <p className="text-xs mt-1">Complexity: {strategy.complexityLevel}/5</p>
                    </TooltipContent>
                  </Tooltip>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {matrix.tiers.map(tier => (
              <tr
                key={tier}
                className={cn(
                  'border-b border-border/30 transition-colors hover:bg-muted/30',
                  tier === 'Admin' && 'bg-amber-50/30 dark:bg-amber-900/10'
                )}
              >
                <td className="px-4 py-4">
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <div
                        className={cn(
                          'flex items-center gap-2 cursor-help font-medium',
                          TIER_COLORS[tier] || ''
                        )}
                      >
                        {TIER_ICONS[tier] || <Users className="h-4 w-4" />}
                        <span>{tier}</span>
                      </div>
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>{TIER_DESCRIPTIONS[tier] || `${tier} tier`}</p>
                    </TooltipContent>
                  </Tooltip>
                </td>
                {matrix.strategies.map(strategy => {
                  const key = `${tier}-${strategy.name}`;
                  const isEnabled = accessMap.get(key) ?? false;
                  const isDefault = defaultMap.get(key) ?? true;
                  const isUpdating =
                    updateMutation.isPending &&
                    updateMutation.variables?.tier === tier &&
                    updateMutation.variables?.strategy === strategy.name;

                  return (
                    <td key={strategy.name} className="px-4 py-4 text-center">
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <div className="inline-flex flex-col items-center gap-1">
                            <Switch
                              checked={isEnabled}
                              onCheckedChange={() => handleToggle(tier, strategy.name, isEnabled)}
                              disabled={isUpdating || tier === 'Anonymous'}
                              className={cn(
                                isEnabled && 'data-[state=checked]:bg-green-600',
                                tier === 'Anonymous' && 'opacity-50 cursor-not-allowed'
                              )}
                              aria-label={`Toggle ${strategy.name} for ${tier}`}
                            />
                            {isDefault && (
                              <span className="text-[10px] text-muted-foreground">default</span>
                            )}
                          </div>
                        </TooltipTrigger>
                        <TooltipContent>
                          <p>
                            {tier === 'Anonymous'
                              ? 'Anonymous users cannot access any strategies'
                              : isEnabled
                                ? `${tier} can access ${strategy.name}`
                                : `${tier} cannot access ${strategy.name}`}
                          </p>
                          {isDefault && <p className="text-xs text-muted-foreground">Using default value</p>}
                        </TooltipContent>
                      </Tooltip>
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Legend */}
      <div className="mt-6 flex flex-wrap gap-4 text-sm text-muted-foreground">
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded bg-green-600" />
          <span>Access enabled</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded bg-muted" />
          <span>Access disabled</span>
        </div>
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-4 w-4 text-amber-500" />
          <span>Admin-only strategy</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] px-1 bg-muted rounded">default</span>
          <span>Using system default</span>
        </div>
      </div>
    </TooltipProvider>
  );
}
