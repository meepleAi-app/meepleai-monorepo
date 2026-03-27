'use client';

/**
 * Configuration Page — Strategy + Models + Limits
 * Consolidates /agents/strategy, /agents/models, /agents/chat-limits into a single
 * tabbed page. Supports deep-linking via ?tab=models and ?tab=limits.
 * Issue #5490 admin-consolidation epic.
 */

import { useState, useMemo, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  MessageSquare,
  Link as LinkIcon,
  Lock,
  RefreshCw,
  Save,
  Settings,
  Target,
  XCircle,
  Zap,
} from 'lucide-react';
import { useSearchParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';
import { StrategyBadge } from '@/components/admin/rag/StrategyBadge';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Switch } from '@/components/ui/forms/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';
import { useAdminConfig, parseConfigValue } from '@/hooks/useAdminConfig';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import { createAdminClient, type StrategyModelMappingDto } from '@/lib/api/clients/adminClient';
import { isNotFoundError } from '@/lib/api/core/errors';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { ChatHistoryLimitsDto } from '@/lib/api/schemas/config.schemas';
import type {
  ModelHealthDto,
  ModelChangeHistoryDto,
} from '@/lib/api/schemas/tier-strategy.schemas';

// ─── Module-level client (shared across tabs) ────────────────────────────────

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Strategy Tab Types ───────────────────────────────────────────────────────

interface RetrievalConfig {
  topK: number;
  minScore: number;
  searchType: 'vector' | 'keyword' | 'hybrid';
  rerankerEnabled: boolean;
  rerankerModel: string;
  cacheTTL: number;
}

interface GenerationConfig {
  provider: string;
  model: string;
  temperature: number;
  maxTokens: number;
  topP: number;
  budgetMode: boolean;
}

interface ConfidenceThresholds {
  lowThreshold: number;
  highThreshold: number;
}

interface ChangedFields {
  retrieval?: Partial<RetrievalConfig>;
  generation?: Partial<GenerationConfig>;
  thresholds?: Partial<ConfidenceThresholds>;
  tierAccess?: Array<{ tier: string; strategy: string; isEnabled: boolean }>;
  modelMappings?: Array<{ strategy: string; data: Partial<StrategyModelMappingDto> }>;
}

// ─── Strategy Tab Fallback Data ───────────────────────────────────────────────

const FALLBACK_PROVIDER_MODELS: Record<string, string[]> = {
  OpenRouter: [
    'anthropic/claude-3.5-sonnet',
    'openai/gpt-4o',
    'meta-llama/llama-3.1-70b',
    'mistralai/mistral-large',
  ],
  OpenAI: ['gpt-4o', 'gpt-4o-mini', 'gpt-4-turbo'],
  Ollama: ['llama3.1:8b', 'llama3.1:70b', 'mistral:7b', 'mixtral:8x7b'],
};

const FALLBACK_RERANKER_MODELS = [
  'cross-encoder/ms-marco-MiniLM-L-6-v2',
  'BAAI/bge-reranker-v2-m3',
];

const FALLBACK_CACHE_TTL_OPTIONS = [
  { value: 15, label: '15 minutes' },
  { value: 30, label: '30 minutes' },
  { value: 60, label: '1 hour' },
  { value: 360, label: '6 hours' },
  { value: 1440, label: '24 hours' },
];

// ─── Models Tab Helpers ───────────────────────────────────────────────────────

function HealthBadge({
  isAvailable,
  isDeprecated,
}: {
  isAvailable: boolean;
  isDeprecated: boolean;
}) {
  if (isDeprecated) {
    return (
      <Badge
        variant="outline"
        className="border-amber-500 text-amber-600 dark:text-amber-400 gap-1"
      >
        <AlertTriangle className="h-3 w-3" aria-label="deprecated" />
        Deprecated
      </Badge>
    );
  }
  if (!isAvailable) {
    return (
      <Badge variant="outline" className="border-red-500 text-red-600 dark:text-red-400 gap-1">
        <XCircle className="h-3 w-3" aria-label="unavailable" />
        Unavailable
      </Badge>
    );
  }
  return (
    <Badge
      variant="outline"
      className="border-emerald-500 text-emerald-600 dark:text-emerald-400 gap-1"
    >
      <CheckCircle2 className="h-3 w-3" aria-label="available" />
      Available
    </Badge>
  );
}

function ChangeTypeBadge({ type }: { type: string }) {
  const styles: Record<string, string> = {
    deprecated: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
    unavailable: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
    restored: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-300',
    replaced: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
    fallback_activated: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300',
    swapped: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300',
  };
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${styles[type] ?? 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-300'}`}
    >
      {type.replace(/_/g, ' ')}
    </span>
  );
}

function timeAgo(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'Just now';
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function ModelHealthTable({ models }: { models: ModelHealthDto[] }) {
  if (models.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        No models tracked yet. Models will appear after the first availability check runs.
      </div>
    );
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 dark:border-zinc-700">
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Model</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Provider</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Status</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Context</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Last Verified</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Alternatives</th>
          </tr>
        </thead>
        <tbody>
          {models.map(model => (
            <tr
              key={model.modelId}
              className="border-b border-slate-100 dark:border-zinc-800 hover:bg-slate-50/50 dark:hover:bg-zinc-800/50 transition-colors"
            >
              <td className="py-3 px-4">
                <div className="font-medium text-foreground">
                  {model.displayName || model.modelId}
                </div>
                <div className="text-xs text-muted-foreground font-mono mt-0.5">
                  {model.modelId}
                </div>
              </td>
              <td className="py-3 px-4 text-muted-foreground capitalize">{model.provider}</td>
              <td className="py-3 px-4">
                <HealthBadge
                  isAvailable={model.isCurrentlyAvailable}
                  isDeprecated={model.isDeprecated}
                />
              </td>
              <td className="py-3 px-4 text-muted-foreground">
                {model.contextWindow > 0 ? `${(model.contextWindow / 1000).toFixed(0)}K` : '—'}
              </td>
              <td className="py-3 px-4">
                <div className="flex items-center gap-1.5 text-muted-foreground">
                  <Clock className="h-3.5 w-3.5" />
                  <span className="text-xs">{timeAgo(model.lastVerifiedAt)}</span>
                </div>
              </td>
              <td className="py-3 px-4 text-xs text-muted-foreground">
                {model.alternatives.length > 0 ? model.alternatives.slice(0, 2).join(', ') : '—'}
                {model.alternatives.length > 2 && ` +${model.alternatives.length - 2}`}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ChangeHistoryTable({ changes }: { changes: ModelChangeHistoryDto[] }) {
  if (changes.length === 0) {
    return (
      <div className="text-center py-8 text-muted-foreground text-sm">
        No model changes recorded yet.
      </div>
    );
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 dark:border-zinc-700">
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">When</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Type</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Model</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Strategy</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Change</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Source</th>
          </tr>
        </thead>
        <tbody>
          {changes.map(change => (
            <tr
              key={change.id}
              className="border-b border-slate-100 dark:border-zinc-800 hover:bg-slate-50/50 dark:hover:bg-zinc-800/50 transition-colors"
            >
              <td className="py-3 px-4 text-xs text-muted-foreground whitespace-nowrap">
                {timeAgo(change.occurredAt)}
              </td>
              <td className="py-3 px-4">
                <ChangeTypeBadge type={change.changeType} />
              </td>
              <td className="py-3 px-4 font-mono text-xs text-foreground">{change.modelId}</td>
              <td className="py-3 px-4 text-muted-foreground">{change.affectedStrategy || '—'}</td>
              <td className="py-3 px-4 text-xs text-muted-foreground max-w-[300px] truncate">
                {change.previousModelId && change.newModelId
                  ? `${change.previousModelId} → ${change.newModelId}`
                  : change.reason}
              </td>
              <td className="py-3 px-4">
                <Badge variant="outline" className="text-xs">
                  {change.isAutomatic ? 'Auto' : 'Admin'}
                </Badge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Chat Limits Schema ───────────────────────────────────────────────────────

const chatHistoryLimitsSchema = z
  .object({
    freeTierLimit: z.number().int().min(1, 'Minimo 1'),
    normalTierLimit: z.number().int().min(1, 'Minimo 1'),
    premiumTierLimit: z.number().int().min(1, 'Minimo 1'),
  })
  .refine(data => data.normalTierLimit >= data.freeTierLimit, {
    message: 'Normal deve essere ≥ Free',
    path: ['normalTierLimit'],
  })
  .refine(data => data.premiumTierLimit >= data.normalTierLimit, {
    message: 'Premium deve essere ≥ Normal',
    path: ['premiumTierLimit'],
  });

type ChatHistoryLimitsForm = z.infer<typeof chatHistoryLimitsSchema>;

// ─── Tab Content Components ───────────────────────────────────────────────────

function StrategyTabContent() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: modelsConfig } = useAdminConfig('models');
  const { data: rerankerConfig } = useAdminConfig('rerankers');
  const { data: strategiesConfig } = useAdminConfig('strategies');

  const PROVIDER_MODELS =
    parseConfigValue<Record<string, string[]>>(modelsConfig, 'strategy_provider_models') ??
    FALLBACK_PROVIDER_MODELS;

  const RERANKER_MODELS =
    parseConfigValue<string[]>(rerankerConfig, 'reranker_models') ?? FALLBACK_RERANKER_MODELS;

  const CACHE_TTL_OPTIONS =
    parseConfigValue<{ value: number; label: string }[]>(strategiesConfig, 'cache_ttl_options') ??
    FALLBACK_CACHE_TTL_OPTIONS;

  const [retrievalConfig, setRetrievalConfig] = useState<RetrievalConfig>({
    topK: 5,
    minScore: 0.65,
    searchType: 'hybrid',
    rerankerEnabled: true,
    rerankerModel: RERANKER_MODELS[0],
    cacheTTL: 30,
  });

  const [generationConfig, setGenerationConfig] = useState<GenerationConfig>({
    provider: 'OpenRouter',
    model: 'anthropic/claude-3.5-sonnet',
    temperature: 0.3,
    maxTokens: 2048,
    topP: 0.9,
    budgetMode: false,
  });

  const [confidenceThresholds, setConfidenceThresholds] = useState<ConfidenceThresholds>({
    lowThreshold: 0.5,
    highThreshold: 0.8,
  });

  const [changedFields, setChangedFields] = useState<ChangedFields>({});
  const [tierAccessChanges, setTierAccessChanges] = useState<Map<string, boolean>>(new Map());

  const {
    data: matrix,
    isLoading: matrixLoading,
    error: matrixError,
    refetch: refetchMatrix,
  } = useQuery({
    queryKey: ['tierStrategyMatrix'],
    queryFn: () => adminClient.getTierStrategyMatrix(),
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const {
    data: modelMappings,
    isLoading: mappingsLoading,
    refetch: refetchMappings,
  } = useQuery({
    queryKey: ['strategyModelMappings'],
    queryFn: () => adminClient.getStrategyModelMappings(),
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const updateAccessMutation = useMutation({
    mutationFn: (payload: { tier: string; strategy: string; isEnabled: boolean }) =>
      adminClient.updateTierStrategyAccess(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tierStrategyMatrix'] });
    },
  });

  const _updateMappingMutation = useMutation({
    mutationFn: (payload: {
      strategy: string;
      provider: string;
      primaryModel: string;
      fallbackModels?: string[];
    }) => adminClient.updateStrategyModelMapping(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['strategyModelMappings'] });
    },
  });

  const hasUnsavedChanges = useMemo(
    () => Object.keys(changedFields).length > 0 || tierAccessChanges.size > 0,
    [changedFields, tierAccessChanges]
  );

  const markRetrievalChanged = (field: keyof RetrievalConfig, value: unknown) => {
    setChangedFields(prev => ({
      ...prev,
      retrieval: { ...prev.retrieval, [field]: value },
    }));
  };

  const markGenerationChanged = (field: keyof GenerationConfig, value: unknown) => {
    setChangedFields(prev => ({
      ...prev,
      generation: { ...prev.generation, [field]: value },
    }));
  };

  const markThresholdsChanged = (field: keyof ConfidenceThresholds, value: number) => {
    setChangedFields(prev => ({
      ...prev,
      thresholds: { ...prev.thresholds, [field]: value },
    }));
  };

  const handleProviderChange = (provider: string) => {
    setGenerationConfig(prev => ({
      ...prev,
      provider,
      model: PROVIDER_MODELS[provider]?.[0] || prev.model,
    }));
    markGenerationChanged('provider', provider);
  };

  const handleTierAccessChange = (tier: string, strategy: string, isEnabled: boolean) => {
    const key = `${tier}-${strategy}`;
    setTierAccessChanges(prev => new Map(prev).set(key, isEnabled));
  };

  const handleSaveAll = async () => {
    try {
      for (const [key, isEnabled] of tierAccessChanges.entries()) {
        const [tier, strategy] = key.split('-');
        await updateAccessMutation.mutateAsync({ tier, strategy, isEnabled });
      }
      toast({
        title: 'Changes saved',
        description: 'Configuration updated successfully',
      });
      setChangedFields({});
      setTierAccessChanges(new Map());
    } catch (error) {
      toast({
        title: 'Save failed',
        description: error instanceof Error ? error.message : 'Unknown error',
        variant: 'destructive',
      });
    }
  };

  const handleDiscard = () => {
    setChangedFields({});
    setTierAccessChanges(new Map());
    refetchMatrix();
    refetchMappings();
  };

  return (
    <div className="space-y-6">
      {/* 404 fallback */}
      {isNotFoundError(matrixError) && (
        <EmptyFeatureState
          title="Funzionalità non disponibile"
          description="Endpoint strategy configuration non ancora implementato nel backend."
        />
      )}

      {/* Overview Cards */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-6">
            <div className="text-center space-y-2">
              <div className="text-3xl">⚡</div>
              <div className="text-2xl font-quicksand font-bold text-amber-600 dark:text-amber-400">
                HybridRAG
              </div>
              <div className="text-xs text-muted-foreground uppercase tracking-wider">
                Active Strategy
              </div>
              <p className="text-xs text-muted-foreground mt-2">
                Combines vector search, keyword matching, and cross-encoder reranking
              </p>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-6">
            <div className="text-center space-y-2">
              <div className="text-3xl">🤖</div>
              <div className="text-2xl font-quicksand font-bold">4</div>
              <div className="text-xs text-muted-foreground uppercase tracking-wider">
                Active Models
              </div>
              <div className="flex justify-center gap-2 mt-3 flex-wrap">
                <Badge className="bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-200">
                  Claude 3.5
                </Badge>
                <Badge className="bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-200">
                  GPT-4o
                </Badge>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-6">
            <div className="text-center space-y-2">
              <div className="text-3xl">📊</div>
              <div className="flex justify-center gap-6 mt-2">
                <div>
                  <div className="text-lg font-quicksand font-bold">0.84</div>
                  <div className="text-xs text-muted-foreground">Avg Confidence</div>
                </div>
                <div>
                  <div className="text-lg font-quicksand font-bold">743ms</div>
                  <div className="text-xs text-muted-foreground">Avg Latency</div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Retrieval & Generation Config */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Retrieval Configuration */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
              <Target className="h-4 w-4 text-blue-500" />
              Retrieval Configuration
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                TopK Results
              </Label>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    const newValue = Math.max(1, retrievalConfig.topK - 1);
                    setRetrievalConfig(prev => ({ ...prev, topK: newValue }));
                    markRetrievalChanged('topK', newValue);
                  }}
                >
                  −
                </Button>
                <Input
                  type="number"
                  min={1}
                  max={20}
                  value={retrievalConfig.topK}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
                    const value = parseInt(e.target.value);
                    setRetrievalConfig(prev => ({ ...prev, topK: value }));
                    markRetrievalChanged('topK', value);
                  }}
                  className="w-20 text-center font-mono"
                />
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    const newValue = Math.min(20, retrievalConfig.topK + 1);
                    setRetrievalConfig(prev => ({ ...prev, topK: newValue }));
                    markRetrievalChanged('topK', newValue);
                  }}
                >
                  +
                </Button>
              </div>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Min Relevance Score
              </Label>
              <div className="flex items-center gap-3">
                <Slider
                  value={[retrievalConfig.minScore * 100]}
                  onValueChange={([value]) => {
                    const score = value / 100;
                    setRetrievalConfig(prev => ({ ...prev, minScore: score }));
                    markRetrievalChanged('minScore', score);
                  }}
                  min={0}
                  max={100}
                  step={1}
                  className="flex-1"
                />
                <Badge className="font-mono w-14 justify-center">
                  {retrievalConfig.minScore.toFixed(2)}
                </Badge>
              </div>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Search Type
              </Label>
              <Select
                value={retrievalConfig.searchType}
                onValueChange={(value: 'vector' | 'keyword' | 'hybrid') => {
                  setRetrievalConfig(prev => ({ ...prev, searchType: value }));
                  markRetrievalChanged('searchType', value);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="vector">Vector Only</SelectItem>
                  <SelectItem value="keyword">Keyword Only</SelectItem>
                  <SelectItem value="hybrid">Hybrid</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Reranker
              </Label>
              <div className="flex items-center gap-3">
                <Switch
                  checked={retrievalConfig.rerankerEnabled}
                  onCheckedChange={checked => {
                    setRetrievalConfig(prev => ({ ...prev, rerankerEnabled: checked }));
                    markRetrievalChanged('rerankerEnabled', checked);
                  }}
                />
                <Select
                  value={retrievalConfig.rerankerModel}
                  onValueChange={value => {
                    setRetrievalConfig(prev => ({ ...prev, rerankerModel: value }));
                    markRetrievalChanged('rerankerModel', value);
                  }}
                  disabled={!retrievalConfig.rerankerEnabled}
                >
                  <SelectTrigger className="flex-1">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {RERANKER_MODELS.map(model => (
                      <SelectItem key={model} value={model}>
                        {model}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Cache TTL
              </Label>
              <Select
                value={retrievalConfig.cacheTTL.toString()}
                onValueChange={value => {
                  const ttl = parseInt(value);
                  setRetrievalConfig(prev => ({ ...prev, cacheTTL: ttl }));
                  markRetrievalChanged('cacheTTL', ttl);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CACHE_TTL_OPTIONS.map(opt => (
                    <SelectItem key={opt.value} value={opt.value.toString()}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        {/* Generation Configuration */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
              <Zap className="h-4 w-4 text-amber-500" />
              Generation Configuration
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Provider
              </Label>
              <Select value={generationConfig.provider} onValueChange={handleProviderChange}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.keys(PROVIDER_MODELS).map(provider => (
                    <SelectItem key={provider} value={provider}>
                      {provider}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Model
              </Label>
              <Select
                value={generationConfig.model}
                onValueChange={value => {
                  setGenerationConfig(prev => ({ ...prev, model: value }));
                  markGenerationChanged('model', value);
                }}
              >
                <SelectTrigger className="font-mono">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PROVIDER_MODELS[generationConfig.provider]?.map(model => (
                    <SelectItem key={model} value={model} className="font-mono">
                      {model}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Temperature
              </Label>
              <div className="flex items-center gap-3">
                <Slider
                  value={[generationConfig.temperature * 100]}
                  onValueChange={([value]) => {
                    const temp = value / 100;
                    setGenerationConfig(prev => ({ ...prev, temperature: temp }));
                    markGenerationChanged('temperature', temp);
                  }}
                  min={0}
                  max={200}
                  step={1}
                  className="flex-1"
                />
                <Badge className="font-mono w-14 justify-center">
                  {generationConfig.temperature.toFixed(2)}
                </Badge>
              </div>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Max Tokens
              </Label>
              <Input
                type="number"
                min={256}
                max={8192}
                step={256}
                value={generationConfig.maxTokens}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
                  const value = parseInt(e.target.value);
                  setGenerationConfig(prev => ({ ...prev, maxTokens: value }));
                  markGenerationChanged('maxTokens', value);
                }}
                className="font-mono"
              />
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Top P
              </Label>
              <div className="flex items-center gap-3">
                <Slider
                  value={[generationConfig.topP * 100]}
                  onValueChange={([value]) => {
                    const topP = value / 100;
                    setGenerationConfig(prev => ({ ...prev, topP }));
                    markGenerationChanged('topP', topP);
                  }}
                  min={0}
                  max={100}
                  step={1}
                  className="flex-1"
                />
                <Badge className="font-mono w-14 justify-center">
                  {generationConfig.topP.toFixed(2)}
                </Badge>
              </div>
            </div>

            <div className="flex items-center justify-between space-x-3">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Budget Mode
              </Label>
              <div className="flex items-center gap-2">
                <Switch
                  checked={generationConfig.budgetMode}
                  onCheckedChange={checked => {
                    setGenerationConfig(prev => ({ ...prev, budgetMode: checked }));
                    markGenerationChanged('budgetMode', checked);
                  }}
                />
                <span className="text-xs text-amber-600 dark:text-amber-400 font-semibold">
                  Uses cheaper models
                </span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Tier Access Matrix */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <Lock className="h-4 w-4 text-purple-500" />
            Tier Access Matrix
          </CardTitle>
        </CardHeader>
        <CardContent>
          {matrixLoading ? (
            <Skeleton className="h-64 w-full" />
          ) : matrix ? (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Role
                    </th>
                    {matrix.strategies.map((strategy: { name: string; displayName: string }) => (
                      <th
                        key={strategy.name}
                        className="text-center p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2"
                      >
                        {strategy.displayName}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {matrix.tiers.map((tier: string) => (
                    <tr key={tier}>
                      <td className="p-3 font-bold text-sm border-b">{tier}</td>
                      {matrix.strategies.map((strategy: { name: string; displayName: string }) => {
                        const access = matrix.accessMatrix.find(
                          (a: { tier: string; strategy: string; isEnabled: boolean }) =>
                            a.tier === tier && a.strategy === strategy.name
                        );
                        const key = `${tier}-${strategy.name}`;
                        const isEnabled = tierAccessChanges.has(key)
                          ? (tierAccessChanges.get(key) ?? false)
                          : (access?.isEnabled ?? false);
                        return (
                          <td key={strategy.name} className="text-center p-3 border-b">
                            <Checkbox
                              checked={isEnabled}
                              onCheckedChange={checked =>
                                handleTierAccessChange(tier, strategy.name, checked as boolean)
                              }
                              className="mx-auto"
                            />
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center text-muted-foreground py-8">No data available</div>
          )}
        </CardContent>
      </Card>

      {/* Strategy-Model Mappings */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <LinkIcon className="h-4 w-4 text-emerald-500" />
            Strategy-Model Mappings
          </CardTitle>
        </CardHeader>
        <CardContent>
          {mappingsLoading ? (
            <Skeleton className="h-64 w-full" />
          ) : modelMappings && modelMappings.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Strategy
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Provider
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Primary Model
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Fallback Models
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {modelMappings.map((mapping: StrategyModelMappingDto) => (
                    <tr key={mapping.strategy}>
                      <td className="p-3 border-b">
                        <StrategyBadge strategy={mapping.strategy} />
                      </td>
                      <td className="p-3 text-sm border-b">{mapping.provider}</td>
                      <td className="p-3 text-sm font-mono border-b">{mapping.primaryModel}</td>
                      <td className="p-3 text-sm font-mono border-b">
                        {mapping.fallbackModels.join(', ')}
                      </td>
                      <td className="p-3 border-b">
                        <Button variant="outline" size="sm">
                          Edit
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center text-muted-foreground py-8">No mappings found</div>
          )}
        </CardContent>
      </Card>

      {/* Confidence Thresholds */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <Settings className="h-4 w-4 text-rose-500" />
            Confidence Thresholds
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <div className="h-4 rounded-full overflow-hidden flex">
              <div
                className="bg-gradient-to-r from-red-500 to-orange-500"
                style={{ flex: confidenceThresholds.lowThreshold * 100 }}
              />
              <div
                className="bg-gradient-to-r from-amber-500 to-yellow-500"
                style={{
                  flex:
                    (confidenceThresholds.highThreshold - confidenceThresholds.lowThreshold) * 100,
                }}
              />
              <div
                className="bg-gradient-to-r from-emerald-500 to-green-500"
                style={{ flex: (1 - confidenceThresholds.highThreshold) * 100 }}
              />
            </div>
            <div className="flex justify-between text-xs text-muted-foreground font-mono">
              <span>Low (&lt; {confidenceThresholds.lowThreshold.toFixed(2)})</span>
              <span>
                Medium ({confidenceThresholds.lowThreshold.toFixed(2)} -{' '}
                {confidenceThresholds.highThreshold.toFixed(2)})
              </span>
              <span>High (&ge; {confidenceThresholds.highThreshold.toFixed(2)})</span>
            </div>
          </div>

          <div className="p-4 rounded-lg bg-slate-50 dark:bg-zinc-900/50 border">
            <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
              Scoring Formula
            </div>
            <code className="text-sm font-semibold text-amber-600 dark:text-amber-400">
              Overall = 0.7 × SearchRelevance + 0.3 × LLMConfidence
            </code>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Low Threshold
              </Label>
              <div className="flex items-center gap-3">
                <Slider
                  value={[confidenceThresholds.lowThreshold * 100]}
                  onValueChange={([value]) => {
                    const threshold = value / 100;
                    setConfidenceThresholds(prev => ({ ...prev, lowThreshold: threshold }));
                    markThresholdsChanged('lowThreshold', threshold);
                  }}
                  min={0}
                  max={100}
                  step={1}
                  className="flex-1"
                />
                <Badge className="font-mono w-14 justify-center bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-200">
                  {confidenceThresholds.lowThreshold.toFixed(2)}
                </Badge>
              </div>
            </div>

            <div className="space-y-2">
              <Label className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                High Threshold
              </Label>
              <div className="flex items-center gap-3">
                <Slider
                  value={[confidenceThresholds.highThreshold * 100]}
                  onValueChange={([value]) => {
                    const threshold = value / 100;
                    setConfidenceThresholds(prev => ({ ...prev, highThreshold: threshold }));
                    markThresholdsChanged('highThreshold', threshold);
                  }}
                  min={0}
                  max={100}
                  step={1}
                  className="flex-1"
                />
                <Badge className="font-mono w-14 justify-center bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-200">
                  {confidenceThresholds.highThreshold.toFixed(2)}
                </Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Unsaved Changes Banner */}
      {hasUnsavedChanges && (
        <div className="fixed bottom-0 left-0 right-0 z-50 bg-amber-600 text-white shadow-2xl">
          <div className="container mx-auto px-6 py-4 flex items-center justify-center gap-4">
            <span className="font-semibold">⚠ You have unsaved changes</span>
            <Button
              variant="secondary"
              size="sm"
              onClick={handleSaveAll}
              className="bg-white text-amber-600 hover:bg-slate-100"
            >
              Save All
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={handleDiscard}
              className="text-white border-2 border-white/50 hover:border-white hover:bg-white/10"
            >
              Discard
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function ModelsTabContent() {
  const queryClient = useQueryClient();
  const [historyLimit] = useState(50);

  const {
    data: healthData,
    isLoading: healthLoading,
    isError: healthError,
  } = useQuery({
    queryKey: ['admin', 'model-health'],
    queryFn: () => adminClient.getModelHealth(),
    refetchInterval: 60_000,
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 1;
    },
  });

  const {
    data: historyData,
    isLoading: historyLoading,
    isError: historyError,
  } = useQuery({
    queryKey: ['admin', 'model-change-history', historyLimit],
    queryFn: () => adminClient.getModelChangeHistory(undefined, historyLimit),
    refetchInterval: 60_000,
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 1;
    },
  });

  const [showCheckSuccess, setShowCheckSuccess] = useState(false);
  const checkNow = useMutation({
    mutationFn: () => adminClient.triggerModelAvailabilityCheck(),
    onSuccess: data => {
      if (data?.triggered) {
        setShowCheckSuccess(true);
        setTimeout(() => {
          setShowCheckSuccess(false);
          queryClient.invalidateQueries({ queryKey: ['admin', 'model-health'] });
          queryClient.invalidateQueries({ queryKey: ['admin', 'model-change-history'] });
        }, 5000);
      }
    },
  });

  const models = healthData?.models ?? [];
  const changes = historyData?.changes ?? [];

  const availableCount = models.filter(
    (m: ModelHealthDto) => m.isCurrentlyAvailable && !m.isDeprecated
  ).length;
  const deprecatedCount = models.filter((m: ModelHealthDto) => m.isDeprecated).length;
  const unavailableCount = models.filter(
    (m: ModelHealthDto) => !m.isCurrentlyAvailable && !m.isDeprecated
  ).length;

  return (
    <div className="space-y-8">
      {/* Actions row */}
      <div className="flex justify-end">
        <Button
          className="bg-amber-500 hover:bg-amber-600 text-white gap-2"
          onClick={() => checkNow.mutate()}
          disabled={checkNow.isPending}
        >
          <RefreshCw className={`h-4 w-4 ${checkNow.isPending ? 'animate-spin' : ''}`} />
          {checkNow.isPending ? 'Checking...' : 'Check Now'}
        </Button>
      </div>

      {/* 404 fallback */}
      {isNotFoundError(healthError) && (
        <EmptyFeatureState
          title="Funzionalità non disponibile"
          description="Endpoint model health non ancora implementato nel backend."
        />
      )}

      {/* Status Summary */}
      {!healthLoading && !healthError && models.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-4">
            <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="h-5 w-5" />
              <span className="text-2xl font-bold">{availableCount}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">Available Models</p>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-4">
            <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400">
              <AlertTriangle className="h-5 w-5" />
              <span className="text-2xl font-bold">{deprecatedCount}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">Deprecated Models</p>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-4">
            <div className="flex items-center gap-2 text-red-600 dark:text-red-400">
              <XCircle className="h-5 w-5" />
              <span className="text-2xl font-bold">{unavailableCount}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">Unavailable Models</p>
          </div>
        </div>
      )}

      {/* Check Now Feedback */}
      {showCheckSuccess && (
        <div className="bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 rounded-lg p-3 text-sm text-emerald-800 dark:text-emerald-300">
          Availability check triggered. Results will update in a few seconds.
        </div>
      )}
      {checkNow.isError && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3 text-sm text-red-800 dark:text-red-300">
          Failed to trigger availability check. Please try again.
        </div>
      )}

      {/* Models Table */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40">
        <div className="px-6 py-4 border-b border-slate-200/60 dark:border-zinc-700/40">
          <h2 className="font-quicksand text-lg font-semibold text-foreground">Tracked Models</h2>
        </div>
        {healthLoading ? (
          <div className="h-[200px] animate-pulse bg-slate-100/50 dark:bg-zinc-700/30 rounded-b-2xl" />
        ) : healthError ? (
          <div className="flex items-center gap-2 p-6 text-red-500">
            <AlertTriangle className="h-5 w-5" />
            Failed to load model health data.
          </div>
        ) : (
          <ModelHealthTable models={models} />
        )}
      </div>

      {/* Change History */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40">
        <div className="px-6 py-4 border-b border-slate-200/60 dark:border-zinc-700/40">
          <h2 className="font-quicksand text-lg font-semibold text-foreground">Change History</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            Audit trail of model swaps, deprecations, and automatic fallbacks
          </p>
        </div>
        {historyLoading ? (
          <div className="h-[200px] animate-pulse bg-slate-100/50 dark:bg-zinc-700/30 rounded-b-2xl" />
        ) : historyError ? (
          <div className="flex items-center gap-2 p-6 text-red-500">
            <AlertTriangle className="h-5 w-5" />
            Failed to load change history.
          </div>
        ) : (
          <ChangeHistoryTable changes={changes} />
        )}
      </div>
    </div>
  );
}

function LimitsTabContent() {
  const [limits, setLimits] = useState<ChatHistoryLimitsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ChatHistoryLimitsForm>({
    resolver: zodResolver(chatHistoryLimitsSchema),
    defaultValues: { freeTierLimit: 10, normalTierLimit: 100, premiumTierLimit: 1000 },
  });

  const loadLimits = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.config.getChatHistoryLimits();
      setLimits(data);
      reset({
        freeTierLimit: data.freeTierLimit,
        normalTierLimit: data.normalTierLimit,
        premiumTierLimit: data.premiumTierLimit,
      });
    } catch {
      setError('Errore nel caricamento dei limiti chat history.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadLimits();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onSubmit = async (data: ChatHistoryLimitsForm) => {
    setSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      const updated = await api.config.updateChatHistoryLimits(data);
      setLimits(updated);
      reset(data);
      setSuccessMsg('Limiti aggiornati con successo.');
    } catch {
      setError('Errore nel salvataggio dei limiti.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-end">
        <Button variant="outline" size="icon" onClick={() => void loadLimits()} disabled={loading}>
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      {successMsg && (
        <Alert>
          <AlertDescription className="text-emerald-600">{successMsg}</AlertDescription>
        </Alert>
      )}

      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <MessageSquare className="h-4 w-4 text-amber-500" />
            Limiti per Tier
          </CardTitle>
          <CardDescription>
            Le chat più vecchie vengono archiviate automaticamente quando si supera il limite (non
            cancellate). Gli admin hanno storia illimitata.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="space-y-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
              <div className="space-y-1.5">
                <Label htmlFor="freeTierLimit" className="font-semibold">
                  Free tier
                </Label>
                <div className="flex items-center gap-3">
                  <Input
                    id="freeTierLimit"
                    type="number"
                    min={1}
                    className="w-36"
                    {...register('freeTierLimit', { valueAsNumber: true })}
                  />
                  <span className="text-sm text-muted-foreground">chat</span>
                </div>
                {errors.freeTierLimit && (
                  <p className="text-xs text-destructive">{errors.freeTierLimit.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="normalTierLimit" className="font-semibold">
                  Normal tier
                </Label>
                <div className="flex items-center gap-3">
                  <Input
                    id="normalTierLimit"
                    type="number"
                    min={1}
                    className="w-36"
                    {...register('normalTierLimit', { valueAsNumber: true })}
                  />
                  <span className="text-sm text-muted-foreground">chat</span>
                </div>
                {errors.normalTierLimit && (
                  <p className="text-xs text-destructive">{errors.normalTierLimit.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="premiumTierLimit" className="font-semibold">
                  Premium tier
                </Label>
                <div className="flex items-center gap-3">
                  <Input
                    id="premiumTierLimit"
                    type="number"
                    min={1}
                    className="w-36"
                    {...register('premiumTierLimit', { valueAsNumber: true })}
                  />
                  <span className="text-sm text-muted-foreground">chat</span>
                </div>
                {errors.premiumTierLimit && (
                  <p className="text-xs text-destructive">{errors.premiumTierLimit.message}</p>
                )}
              </div>

              <div className="rounded-lg border border-border/50 bg-muted/30 px-4 py-3">
                <div className="flex items-center gap-3">
                  <span className="font-semibold text-sm">Admin</span>
                  <span className="text-sm text-muted-foreground">Illimitato</span>
                </div>
              </div>

              <div className="flex items-center justify-between pt-2 border-t border-border/50">
                <Button type="submit" disabled={!isDirty || submitting} className="gap-2">
                  {submitting ? (
                    <RefreshCw className="h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="h-4 w-4" />
                  )}
                  {submitting ? 'Salvataggio...' : 'Salva'}
                </Button>

                {limits && (
                  <p className="text-xs text-muted-foreground">
                    Aggiornato il{' '}
                    {new Date(limits.lastUpdatedAt).toLocaleString('it-IT', {
                      day: '2-digit',
                      month: '2-digit',
                      year: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </p>
                )}
              </div>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

// ─── Valid tab values ─────────────────────────────────────────────────────────

type TabValue = 'strategy' | 'models' | 'limits';

const VALID_TABS: TabValue[] = ['strategy', 'models', 'limits'];

function isValidTab(value: string | null): value is TabValue {
  return VALID_TABS.includes(value as TabValue);
}

// ─── Page ────────────────────────────────────────────────────────────────────

export default function ConfigurationPage() {
  const searchParams = useSearchParams();
  const tabParam = searchParams.get('tab');
  const defaultTab: TabValue = isValidTab(tabParam) ? tabParam : 'strategy';

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Configurazione AI
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Strategy, modelli e limiti degli agenti
        </p>
      </div>

      <Tabs defaultValue={defaultTab}>
        <TabsList>
          <TabsTrigger value="strategy">Strategy</TabsTrigger>
          <TabsTrigger value="models">Models</TabsTrigger>
          <TabsTrigger value="limits">Limits</TabsTrigger>
        </TabsList>

        <TabsContent value="strategy" className="mt-6">
          <StrategyTabContent />
        </TabsContent>

        <TabsContent value="models" className="mt-6">
          <ModelsTabContent />
        </TabsContent>

        <TabsContent value="limits" className="mt-6">
          <LimitsTabContent />
        </TabsContent>
      </Tabs>
    </div>
  );
}
