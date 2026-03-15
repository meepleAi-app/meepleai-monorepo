'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Brain, Crown, GitBranch, RefreshCwIcon, Search, TreePine, Users, Zap } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { HttpClient } from '@/lib/api/core/httpClient';
import { cn } from '@/lib/utils';

const httpClient = new HttpClient();

interface RagEnhancementDto {
  name: string;
  flagKey: string;
  isGloballyEnabled: boolean;
  extraCreditsBalanced: number;
  extraCreditsFast: number;
  tierAccess?: Record<string, boolean>;
}

const ENHANCEMENT_META: Record<
  string,
  { icon: React.ReactNode; description: string; impact: string }
> = {
  AdaptiveRouting: {
    icon: <Brain className="h-4 w-4" />,
    description:
      'Classifica la complessita della query per saltare il retrieval su domande semplici',
    impact: '+7-10% accuracy, risparmia token',
  },
  CragEvaluation: {
    icon: <Search className="h-4 w-4" />,
    description: 'Valuta la rilevanza dei chunk prima della generazione, ri-cerca se irrilevanti',
    impact: '+3-5% accuracy, riduce allucinazioni',
  },
  RaptorRetrieval: {
    icon: <TreePine className="h-4 w-4" />,
    description: 'Indicizzazione gerarchica per retrieval multi-granularita (overview → dettaglio)',
    impact: '+5-8% su domande ampie',
  },
  RagFusionQueries: {
    icon: <GitBranch className="h-4 w-4" />,
    description: 'Genera varianti della query per catturare risultati da angolazioni diverse',
    impact: '+2-3% su query ambigue',
  },
  GraphTraversal: {
    icon: <Zap className="h-4 w-4" />,
    description: 'Estrae entita e relazioni per query relazionali (giochi simili, meccaniche)',
    impact: 'Abilita nuovi tipi di query',
  },
};

const TIERS = ['Free', 'Basic', 'Pro'] as const;
type Tier = (typeof TIERS)[number];

const TIER_ICONS: Record<Tier, React.ReactNode> = {
  Free: <Users className="h-3.5 w-3.5" />,
  Basic: <Zap className="h-3.5 w-3.5" />,
  Pro: <Crown className="h-3.5 w-3.5" />,
};

const TIER_COLORS: Record<Tier, string> = {
  Free: 'text-slate-600 dark:text-slate-400',
  Basic: 'text-blue-600 dark:text-blue-400',
  Pro: 'text-amber-600 dark:text-amber-400',
};

const TIER_SWITCH_COLORS: Record<Tier, string> = {
  Free: 'data-[state=checked]:bg-slate-500',
  Basic: 'data-[state=checked]:bg-blue-500',
  Pro: 'data-[state=checked]:bg-amber-500',
};

function EnhancementSkeleton() {
  return (
    <div className="space-y-4">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="h-36 bg-white/40 dark:bg-zinc-800/40 rounded-xl animate-pulse" />
      ))}
    </div>
  );
}

export function RagEnhancementsTab() {
  const queryClient = useQueryClient();

  const {
    data: enhancements,
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'rag-enhancements'],
    queryFn: () => httpClient.get<RagEnhancementDto[]>('/api/v1/admin/rag-enhancements'),
    staleTime: 300_000,
  });

  const globalToggleMutation = useMutation({
    mutationFn: (flagKey: string) =>
      httpClient.post<void>(`/api/v1/admin/rag-enhancements/${flagKey}/toggle`),
    onMutate: async flagKey => {
      await queryClient.cancelQueries({ queryKey: ['admin', 'rag-enhancements'] });

      const previousData = queryClient.getQueryData<RagEnhancementDto[]>([
        'admin',
        'rag-enhancements',
      ]);

      queryClient.setQueryData<RagEnhancementDto[]>(['admin', 'rag-enhancements'], old =>
        old?.map(e =>
          e.flagKey === flagKey ? { ...e, isGloballyEnabled: !e.isGloballyEnabled } : e
        )
      );

      return { previousData };
    },
    onError: (_err, _flagKey, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(['admin', 'rag-enhancements'], context.previousData);
      }
      toast.error('Failed to toggle enhancement');
    },
    onSuccess: (_data, flagKey) => {
      const updated = queryClient.getQueryData<RagEnhancementDto[]>(['admin', 'rag-enhancements']);
      const enhancement = updated?.find(e => e.flagKey === flagKey);
      if (enhancement) {
        toast.success(
          `${enhancement.name} ${enhancement.isGloballyEnabled ? 'enabled' : 'disabled'}`
        );
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'rag-enhancements'] });
    },
  });

  const tierToggleMutation = useMutation({
    mutationFn: ({ flagKey, tier }: { flagKey: string; tier: string }) =>
      httpClient.post<void>(`/api/v1/admin/rag-enhancements/${flagKey}/tier/${tier}/toggle`),
    onMutate: async ({ flagKey, tier }) => {
      await queryClient.cancelQueries({ queryKey: ['admin', 'rag-enhancements'] });

      const previousData = queryClient.getQueryData<RagEnhancementDto[]>([
        'admin',
        'rag-enhancements',
      ]);

      queryClient.setQueryData<RagEnhancementDto[]>(['admin', 'rag-enhancements'], old =>
        old?.map(e => {
          if (e.flagKey !== flagKey) return e;
          const currentAccess = e.tierAccess ?? {};
          return {
            ...e,
            tierAccess: {
              ...currentAccess,
              [tier]: !currentAccess[tier],
            },
          };
        })
      );

      return { previousData };
    },
    onError: (_err, _vars, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(['admin', 'rag-enhancements'], context.previousData);
      }
      toast.error('Failed to toggle tier access');
    },
    onSuccess: (_data, { flagKey, tier }) => {
      toast.success(`${tier} tier updated for ${flagKey}`);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'rag-enhancements'] });
    },
  });

  if (isLoading) {
    return <EnhancementSkeleton />;
  }

  if (!enhancements || enhancements.length === 0) {
    return (
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <p className="text-sm text-slate-600 dark:text-zinc-400">
          No RAG enhancements configured. They will appear here once the backend is set up.
        </p>
      </div>
    );
  }

  return (
    <TooltipProvider>
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-2">
            <Brain className="h-5 w-5 text-purple-500" />
            <h3 className="font-semibold text-slate-900 dark:text-zinc-100">RAG Enhancements</h3>
            <Badge variant="outline" className="text-xs">
              {enhancements.filter(e => e.isGloballyEnabled).length}/{enhancements.length} active
            </Badge>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-2"
          >
            <RefreshCwIcon className={cn('h-4 w-4', isRefetching && 'animate-spin')} />
            Refresh
          </Button>
        </div>

        {/* Enhancement Cards */}
        <div className="space-y-4">
          {enhancements.map(enhancement => {
            const meta = ENHANCEMENT_META[enhancement.flagKey] ?? {
              icon: <Zap className="h-4 w-4" />,
              description: enhancement.name,
              impact: 'N/A',
            };

            return (
              <div
                key={enhancement.flagKey}
                className={cn(
                  'rounded-lg border p-4 transition-colors',
                  enhancement.isGloballyEnabled
                    ? 'border-purple-200/50 dark:border-purple-800/50 bg-purple-50/30 dark:bg-purple-900/10'
                    : 'border-slate-200/50 dark:border-zinc-700/50 bg-slate-50/30 dark:bg-zinc-900/20'
                )}
              >
                {/* Top row: Icon + Name + Global toggle */}
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span
                      className={cn(
                        enhancement.isGloballyEnabled
                          ? 'text-purple-600 dark:text-purple-400'
                          : 'text-slate-400 dark:text-zinc-500'
                      )}
                    >
                      {meta.icon}
                    </span>
                    <span className="font-medium text-slate-900 dark:text-zinc-100">
                      {enhancement.name}
                    </span>
                  </div>
                  <Switch
                    checked={enhancement.isGloballyEnabled}
                    onCheckedChange={() => globalToggleMutation.mutate(enhancement.flagKey)}
                    disabled={globalToggleMutation.isPending}
                    aria-label={`Toggle ${enhancement.name}`}
                    className={
                      enhancement.isGloballyEnabled ? 'data-[state=checked]:bg-purple-600' : ''
                    }
                  />
                </div>

                {/* Description */}
                <p className="text-sm text-slate-600 dark:text-zinc-400 mb-3">{meta.description}</p>

                {/* Tier Access row */}
                <div className="flex items-center gap-4 mb-3">
                  <span className="text-xs font-medium text-slate-500 dark:text-zinc-500">
                    Tier Access:
                  </span>
                  <div className="flex items-center gap-3">
                    {TIERS.map(tier => {
                      const tierEnabled = enhancement.tierAccess?.[tier] ?? false;
                      return (
                        <Tooltip key={tier}>
                          <TooltipTrigger asChild>
                            <label
                              className={cn(
                                'flex items-center gap-1.5 cursor-pointer',
                                TIER_COLORS[tier]
                              )}
                            >
                              {TIER_ICONS[tier]}
                              <span className="text-xs font-medium">{tier}</span>
                              <Switch
                                checked={tierEnabled}
                                onCheckedChange={() =>
                                  tierToggleMutation.mutate({
                                    flagKey: enhancement.flagKey,
                                    tier,
                                  })
                                }
                                disabled={
                                  !enhancement.isGloballyEnabled || tierToggleMutation.isPending
                                }
                                aria-label={`Toggle ${tier} tier for ${enhancement.name}`}
                                className={cn('scale-75', tierEnabled && TIER_SWITCH_COLORS[tier])}
                              />
                            </label>
                          </TooltipTrigger>
                          <TooltipContent>
                            <p>
                              {tierEnabled ? 'Enabled' : 'Disabled'} for {tier} tier
                            </p>
                          </TooltipContent>
                        </Tooltip>
                      );
                    })}
                  </div>
                </div>

                {/* Cost and Impact badges */}
                <div className="flex flex-wrap items-center gap-2">
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Badge
                        variant="outline"
                        className="text-xs text-slate-600 dark:text-zinc-400"
                      >
                        +{enhancement.extraCreditsFast} credits (FAST)
                      </Badge>
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>Extra credits consumed in FAST mode</p>
                    </TooltipContent>
                  </Tooltip>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Badge
                        variant="outline"
                        className="text-xs text-slate-600 dark:text-zinc-400"
                      >
                        +{enhancement.extraCreditsBalanced} credits (BALANCED)
                      </Badge>
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>Extra credits consumed in BALANCED mode</p>
                    </TooltipContent>
                  </Tooltip>
                  <Badge variant="secondary" className="text-xs">
                    {meta.impact}
                  </Badge>
                </div>
              </div>
            );
          })}
        </div>

        {/* Help text */}
        <div className="mt-6 bg-slate-50/70 dark:bg-zinc-900/50 rounded-lg p-4 border border-slate-200/30 dark:border-zinc-700/30">
          <h4 className="text-sm font-medium text-slate-700 dark:text-zinc-300 mb-2">
            RAG Enhancements Guide
          </h4>
          <ul className="text-xs text-slate-500 dark:text-zinc-500 space-y-1">
            <li>Toggle enhancements globally, then control per-tier access</li>
            <li>Tier toggles are disabled when the enhancement is globally off</li>
            <li>Credits show the extra cost per query for each mode (FAST vs BALANCED)</li>
            <li>Impact estimates are approximate and may vary by query type</li>
          </ul>
        </div>
      </div>
    </TooltipProvider>
  );
}
