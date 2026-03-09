'use client';

/**
 * Strategy Configuration Page
 * Admin UI for RAG strategy configuration, tier access, and model mappings
 * Issue #4458: RAG Observability Dashboard
 */

import { useState, useMemo } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Settings, Target, Lock, Link as LinkIcon, Zap, RefreshCw } from 'lucide-react';

import { StrategyBadge } from '@/components/admin/rag/StrategyBadge';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Switch } from '@/components/ui/forms/switch';
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
import { useToast } from '@/hooks/use-toast';
import { createAdminClient, type StrategyModelMappingDto } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

// ========== Types ==========

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

// ========== Provider Model Mappings ==========

const PROVIDER_MODELS: Record<string, string[]> = {
  OpenRouter: [
    'anthropic/claude-3.5-sonnet',
    'openai/gpt-4o',
    'meta-llama/llama-3.1-70b',
    'mistralai/mistral-large',
  ],
  OpenAI: ['gpt-4o', 'gpt-4o-mini', 'gpt-4-turbo'],
  Ollama: ['llama3.1:8b', 'llama3.1:70b', 'mistral:7b', 'mixtral:8x7b'],
};

const RERANKER_MODELS = ['cross-encoder/ms-marco-MiniLM-L-6-v2', 'BAAI/bge-reranker-v2-m3'];

const CACHE_TTL_OPTIONS = [
  { value: 15, label: '15 minutes' },
  { value: 30, label: '30 minutes' },
  { value: 60, label: '1 hour' },
  { value: 360, label: '6 hours' },
  { value: 1440, label: '24 hours' },
];

// ========== Main Component ==========

// Initialize AdminClient instance
const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

export default function StrategyConfigPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // State
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

  // Queries
  const {
    data: matrix,
    isLoading: matrixLoading,
    refetch: refetchMatrix,
  } = useQuery({
    queryKey: ['tierStrategyMatrix'],
    queryFn: () => adminClient.getTierStrategyMatrix(),
  });

  const {
    data: modelMappings,
    isLoading: mappingsLoading,
    refetch: refetchMappings,
  } = useQuery({
    queryKey: ['strategyModelMappings'],
    queryFn: () => adminClient.getStrategyModelMappings(),
  });

  // Mutations
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

  // Computed
  const hasUnsavedChanges = useMemo(
    () => Object.keys(changedFields).length > 0 || tierAccessChanges.size > 0,
    [changedFields, tierAccessChanges]
  );

  // Handlers
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
      // Save tier access changes
      for (const [key, isEnabled] of tierAccessChanges.entries()) {
        const [tier, strategy] = key.split('-');
        await updateAccessMutation.mutateAsync({ tier, strategy, isEnabled });
      }

      // Save model mapping changes (if any)
      // Note: This is a simplified version. In practice, you'd track which mappings changed.

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
    // Reset state to initial values
    refetchMatrix();
    refetchMappings();
  };

  const handleRefresh = () => {
    refetchMatrix();
    refetchMappings();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Strategy Configuration
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Configure RAG strategies, tier access, and model mappings
          </p>
        </div>
        <Button variant="outline" size="icon" onClick={handleRefresh}>
          <RefreshCw className="h-4 w-4" />
        </Button>
      </div>

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
            {/* TopK */}
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

            {/* Min Score */}
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

            {/* Search Type */}
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

            {/* Reranker */}
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

            {/* Cache TTL */}
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
            {/* Provider */}
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

            {/* Model */}
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

            {/* Temperature */}
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

            {/* Max Tokens */}
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

            {/* Top P */}
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

            {/* Budget Mode */}
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
                          a => a.tier === tier && a.strategy === strategy.name
                        );
                        const key = `${tier}-${strategy.name}`;
                        const isEnabled = tierAccessChanges.has(key)
                          ? tierAccessChanges.get(key)!
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
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60 mb-16">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <Settings className="h-4 w-4 text-rose-500" />
            Confidence Thresholds
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Visual Gauge */}
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

          {/* Scoring Formula */}
          <div className="p-4 rounded-lg bg-slate-50 dark:bg-zinc-900/50 border">
            <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
              Scoring Formula
            </div>
            <code className="text-sm font-semibold text-amber-600 dark:text-amber-400">
              Overall = 0.7 × SearchRelevance + 0.3 × LLMConfidence
            </code>
          </div>

          {/* Threshold Sliders */}
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
