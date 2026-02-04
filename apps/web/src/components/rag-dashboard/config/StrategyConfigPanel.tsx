'use client';

import React, { useState, useCallback } from 'react';

import { Settings, RotateCcw, Save, AlertCircle, Loader2 } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Alert,
  AlertDescription,
} from '@/components/ui/feedback/alert';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import { GenerationParams } from './GenerationParams';
import { ModelSelector } from './ModelSelector';
import { RerankerSettings } from './RerankerSettings';
import { RetrievalParams } from './RetrievalParams';
import { StrategySpecificSettings } from './StrategySpecificSettings';
import { DEFAULT_RAG_CONFIG, STRATEGY_PRESETS } from './types';

import type { RagConfig } from './types';
import type { RetrievalStrategyType } from '../retrieval-strategies';

export interface StrategyConfigPanelProps {
  /** Initial configuration to load */
  initialConfig?: RagConfig;
  /** Active retrieval strategy */
  activeStrategy: RetrievalStrategyType;
  /** Called when configuration changes */
  onChange?: (config: RagConfig) => void;
  /** Called when save button is clicked */
  onSave?: (config: RagConfig) => Promise<void>;
  /** Called when reset button is clicked */
  onReset?: () => void;
  /** Whether the panel is in a loading state */
  isLoading?: boolean;
  /** Whether there are unsaved changes */
  isDirty?: boolean;
  /** Error message to display */
  error?: string | null;
  /** Additional class names */
  className?: string;
}

/**
 * StrategyConfigPanel Component
 *
 * Main configuration panel for RAG strategy parameters.
 * Organizes settings into tabbed sections:
 * - Generation: LLM generation parameters
 * - Retrieval: Document retrieval parameters
 * - Models: LLM model selection
 * - Advanced: Reranker and strategy-specific settings
 */
export function StrategyConfigPanel({
  initialConfig = DEFAULT_RAG_CONFIG,
  activeStrategy,
  onChange,
  onSave,
  onReset,
  isLoading = false,
  isDirty = false,
  error = null,
  className,
}: StrategyConfigPanelProps): React.JSX.Element {
  const [config, setConfig] = useState<RagConfig>(() => ({
    ...initialConfig,
    activeStrategy,
  }));
  const [isSaving, setIsSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('generation');

  // Update config when activeStrategy changes from parent
  React.useEffect(() => {
    if (config.activeStrategy !== activeStrategy) {
      const preset = STRATEGY_PRESETS[activeStrategy];
      setConfig((prev) => ({
        ...prev,
        ...preset,
        activeStrategy,
      }));
    }
  }, [activeStrategy, config.activeStrategy]);

  const handleConfigChange = useCallback(
    (updates: Partial<RagConfig>) => {
      setConfig((prev) => {
        const newConfig = { ...prev, ...updates };
        onChange?.(newConfig);
        return newConfig;
      });
    },
    [onChange]
  );

  const handleSave = useCallback(async () => {
    if (!onSave) return;
    setIsSaving(true);
    try {
      await onSave(config);
    } finally {
      setIsSaving(false);
    }
  }, [config, onSave]);

  const handleReset = useCallback(() => {
    const preset = STRATEGY_PRESETS[activeStrategy];
    const resetConfig: RagConfig = {
      ...DEFAULT_RAG_CONFIG,
      ...preset,
      activeStrategy,
    };
    setConfig(resetConfig);
    onChange?.(resetConfig);
    onReset?.();
  }, [activeStrategy, onChange, onReset]);

  const showEvaluationModel = activeStrategy === 'Agentic' || activeStrategy === 'MultiQuery';

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-lg">
            <Settings className="h-5 w-5 text-primary" />
            Strategy Configuration
          </CardTitle>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleReset}
              disabled={isLoading || isSaving}
            >
              <RotateCcw className="h-4 w-4 mr-1" />
              Reset
            </Button>
            {onSave && (
              <Button
                size="sm"
                onClick={handleSave}
                disabled={isLoading || isSaving || !isDirty}
              >
                {isSaving ? (
                  <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                ) : (
                  <Save className="h-4 w-4 mr-1" />
                )}
                Save
              </Button>
            )}
          </div>
        </div>
        {isDirty && (
          <p className="text-xs text-muted-foreground mt-1">
            You have unsaved changes
          </p>
        )}
      </CardHeader>

      <CardContent>
        {error && (
          <Alert variant="destructive" className="mb-4">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
          <TabsList className="grid w-full grid-cols-4 mb-4">
            <TabsTrigger value="generation">Generation</TabsTrigger>
            <TabsTrigger value="retrieval">Retrieval</TabsTrigger>
            <TabsTrigger value="models">Models</TabsTrigger>
            <TabsTrigger value="advanced">Advanced</TabsTrigger>
          </TabsList>

          <TabsContent value="generation" className="mt-0">
            <GenerationParams
              params={config.generation}
              onChange={(generation) => handleConfigChange({ generation })}
            />
          </TabsContent>

          <TabsContent value="retrieval" className="mt-0">
            <RetrievalParams
              params={config.retrieval}
              onChange={(retrieval) => handleConfigChange({ retrieval })}
            />
          </TabsContent>

          <TabsContent value="models" className="mt-0">
            <ModelSelector
              selection={config.models}
              onChange={(models) => handleConfigChange({ models })}
              showEvaluationModel={showEvaluationModel}
            />
          </TabsContent>

          <TabsContent value="advanced" className="mt-0 space-y-6">
            <div>
              <h3 className="text-sm font-medium mb-4">Reranker Settings</h3>
              <RerankerSettings
                settings={config.reranker}
                onChange={(reranker) => handleConfigChange({ reranker })}
              />
            </div>

            <div className="border-t pt-6">
              <h3 className="text-sm font-medium mb-4">
                Strategy-Specific Settings
                <span className="ml-2 text-xs font-normal text-muted-foreground">
                  ({activeStrategy})
                </span>
              </h3>
              <StrategySpecificSettings
                settings={config.strategySpecific}
                onChange={(strategySpecific) => handleConfigChange({ strategySpecific })}
                activeStrategy={activeStrategy}
              />
            </div>
          </TabsContent>
        </Tabs>

        {isLoading && (
          <div className="absolute inset-0 bg-background/80 flex items-center justify-center rounded-lg">
            <Loader2 className="h-8 w-8 animate-spin text-primary" />
          </div>
        )}
      </CardContent>
    </Card>
  );
}
