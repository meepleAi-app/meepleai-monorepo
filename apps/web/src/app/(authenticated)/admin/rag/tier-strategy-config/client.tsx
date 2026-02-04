'use client';

/**
 * Tier-Strategy Configuration Client Component
 * Issue #3440: Admin UI for tier-strategy configuration
 */

import { useState } from 'react';

import { RefreshCw, Settings, Table2, RotateCcw } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import {
  useTierStrategyMatrix,
  useStrategyModelMappings,
  useResetTierStrategyConfig,
} from '@/hooks/queries/useTierStrategy';

import { StrategyModelMappingEditor } from './StrategyModelMappingEditor';
import { TierStrategyMatrix } from './TierStrategyMatrix';

export function TierStrategyConfigClient() {
  const [activeTab, setActiveTab] = useState('matrix');

  const { data: matrix, isLoading: matrixLoading, refetch: refetchMatrix } = useTierStrategyMatrix();
  const {
    data: modelMappings,
    isLoading: mappingsLoading,
    refetch: refetchMappings,
  } = useStrategyModelMappings();
  const resetMutation = useResetTierStrategyConfig();

  const handleRefresh = async () => {
    await Promise.all([refetchMatrix(), refetchMappings()]);
    toast.success('Configuration refreshed');
  };

  const handleReset = async () => {
    const confirmed = window.confirm(
      'Are you sure you want to reset all tier-strategy configuration to defaults?\n\n' +
        'This will delete all custom access rules and model mappings.'
    );

    if (!confirmed) return;

    try {
      const result = await resetMutation.mutateAsync(undefined);
      toast.success(result.message);
    } catch (_error) {
      toast.error('Failed to reset configuration');
    }
  };

  const isLoading = matrixLoading || mappingsLoading;

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Tier-Strategy Configuration</h1>
          <p className="text-muted-foreground mt-1">
            Configure which user tiers can access which RAG strategies
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={handleRefresh} disabled={isLoading}>
            <RefreshCw className={`h-4 w-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button
            variant="destructive"
            size="sm"
            onClick={handleReset}
            disabled={resetMutation.isPending}
          >
            <RotateCcw className="h-4 w-4 mr-2" />
            Reset to Defaults
          </Button>
        </div>
      </div>

      {/* Info Card */}
      <Card className="bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800">
        <CardHeader className="pb-2">
          <CardTitle className="text-lg text-blue-900 dark:text-blue-100 flex items-center gap-2">
            <Settings className="h-5 w-5" />
            How Tier-Strategy Access Works
          </CardTitle>
        </CardHeader>
        <CardContent>
          <ul className="text-sm text-blue-800 dark:text-blue-200 space-y-1">
            <li>• <strong>User Tier</strong> determines which strategies are available (access control)</li>
            <li>• <strong>Strategy</strong> determines which LLM model is used (quality/cost tradeoff)</li>
            <li>• Higher tiers can access more complex strategies with better models</li>
            <li>• Defaults are restored if no custom configuration exists</li>
          </ul>
        </CardContent>
      </Card>

      {/* Main Content */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid w-full grid-cols-2 max-w-md">
          <TabsTrigger value="matrix" className="flex items-center gap-2">
            <Table2 className="h-4 w-4" />
            Access Matrix
          </TabsTrigger>
          <TabsTrigger value="mappings" className="flex items-center gap-2">
            <Settings className="h-4 w-4" />
            Model Mappings
          </TabsTrigger>
        </TabsList>

        <TabsContent value="matrix" className="mt-6">
          <Card>
            <CardHeader>
              <CardTitle>Tier-Strategy Access Matrix</CardTitle>
              <CardDescription>
                Toggle which user tiers can access which RAG strategies
              </CardDescription>
            </CardHeader>
            <CardContent>
              {matrixLoading ? (
                <div className="flex items-center justify-center py-8">
                  <RefreshCw className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : matrix ? (
                <TierStrategyMatrix matrix={matrix} />
              ) : (
                <p className="text-muted-foreground">Failed to load matrix</p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="mappings" className="mt-6">
          <Card>
            <CardHeader>
              <CardTitle>Strategy-Model Mappings</CardTitle>
              <CardDescription>
                Configure which LLM models are used for each RAG strategy
              </CardDescription>
            </CardHeader>
            <CardContent>
              {mappingsLoading ? (
                <div className="flex items-center justify-center py-8">
                  <RefreshCw className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : modelMappings ? (
                <StrategyModelMappingEditor mappings={modelMappings} />
              ) : (
                <p className="text-muted-foreground">Failed to load mappings</p>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
