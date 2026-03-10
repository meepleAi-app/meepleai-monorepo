'use client';

/**
 * New A/B Test page — Create a blind model comparison.
 * Issue #5500: A/B Testing frontend — New Test page.
 *
 * Flow: Select models (2-4) → Enter query → Generate → Redirect to evaluation.
 */

import { useState, useCallback } from 'react';

import { FlaskConical, Loader2, Sparkles, AlertCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';

import { AgentsNavConfig } from '../../NavConfig';

// Available models for A/B testing (OpenRouter format)
const AVAILABLE_MODELS = [
  { id: 'openai/gpt-4o', provider: 'OpenAI', name: 'GPT-4o' },
  { id: 'openai/gpt-4o-mini', provider: 'OpenAI', name: 'GPT-4o Mini' },
  { id: 'anthropic/claude-3.5-sonnet', provider: 'Anthropic', name: 'Claude 3.5 Sonnet' },
  { id: 'anthropic/claude-3-haiku', provider: 'Anthropic', name: 'Claude 3 Haiku' },
  { id: 'google/gemini-2.0-flash', provider: 'Google', name: 'Gemini 2.0 Flash' },
  { id: 'meta-llama/llama-3.1-70b-instruct', provider: 'Meta', name: 'Llama 3.1 70B' },
  { id: 'mistralai/mistral-large-latest', provider: 'Mistral', name: 'Mistral Large' },
  { id: 'qwen/qwen-2.5-72b-instruct', provider: 'Qwen', name: 'Qwen 2.5 72B' },
] as const;

const MIN_MODELS = 2;
const MAX_MODELS = 4;

export default function NewAbTestPage() {
  const router = useRouter();
  const [query, setQuery] = useState('');
  const [selectedModels, setSelectedModels] = useState<string[]>([]);
  const [isGenerating, setIsGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit =
    query.trim().length > 0 &&
    selectedModels.length >= MIN_MODELS &&
    selectedModels.length <= MAX_MODELS &&
    !isGenerating;

  const handleModelToggle = useCallback((modelId: string) => {
    setSelectedModels(prev => {
      if (prev.includes(modelId)) {
        return prev.filter(id => id !== modelId);
      }
      if (prev.length >= MAX_MODELS) return prev;
      return [...prev, modelId];
    });
  }, []);

  const handleGenerate = useCallback(async () => {
    if (!canSubmit) return;
    setIsGenerating(true);
    setError(null);

    try {
      const result = await api.admin.createAbTest({
        query: query.trim(),
        modelIds: selectedModels,
      });
      router.push(`/admin/agents/ab-testing/${result.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create A/B test');
      setIsGenerating(false);
    }
  }, [canSubmit, query, selectedModels, router]);

  return (
    <div className="space-y-8">
      <AgentsNavConfig />

      {/* Header */}
      <div>
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 dark:bg-amber-900/30">
            <FlaskConical className="h-5 w-5 text-amber-600 dark:text-amber-400" />
          </div>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              New A/B Test
            </h1>
            <p className="text-sm text-muted-foreground">
              Compare model responses blindly, then evaluate
            </p>
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-8 lg:grid-cols-2">
        {/* Left: Query */}
        <div className="space-y-4">
          <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-6 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
            <Label htmlFor="query" className="text-base font-semibold">
              Question / Prompt
            </Label>
            <p className="mt-1 text-sm text-muted-foreground">
              Enter the question you want each model to answer
            </p>
            <Textarea
              id="query"
              placeholder="e.g. What are the rules for setting up a game of Catan?"
              className="mt-3 min-h-[160px] resize-y"
              value={query}
              onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => setQuery(e.target.value)}
              maxLength={2000}
              disabled={isGenerating}
            />
            <p className="mt-2 text-xs text-muted-foreground text-right">{query.length}/2000</p>
          </div>
        </div>

        {/* Right: Model Selection */}
        <div className="space-y-4">
          <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-6 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
            <Label className="text-base font-semibold">Select Models</Label>
            <p className="mt-1 text-sm text-muted-foreground">
              Choose {MIN_MODELS}-{MAX_MODELS} models to compare ({selectedModels.length} selected)
            </p>

            <div className="mt-4 space-y-3">
              {AVAILABLE_MODELS.map(model => {
                const isSelected = selectedModels.includes(model.id);
                const isDisabled =
                  isGenerating || (!isSelected && selectedModels.length >= MAX_MODELS);

                return (
                  <label
                    key={model.id}
                    className={`flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition-colors ${
                      isSelected
                        ? 'border-amber-300 bg-amber-50/50 dark:border-amber-700 dark:bg-amber-900/20'
                        : 'border-slate-200/60 hover:border-slate-300 dark:border-zinc-700/40 dark:hover:border-zinc-600'
                    } ${isDisabled && !isSelected ? 'cursor-not-allowed opacity-50' : ''}`}
                  >
                    <Checkbox
                      checked={isSelected}
                      onCheckedChange={() => handleModelToggle(model.id)}
                      disabled={isDisabled}
                    />
                    <div className="flex-1">
                      <span className="font-medium text-sm">{model.name}</span>
                      <span className="ml-2 text-xs text-muted-foreground">{model.provider}</span>
                    </div>
                  </label>
                );
              })}
            </div>

            {selectedModels.length > 0 && selectedModels.length < MIN_MODELS && (
              <p className="mt-3 text-xs text-amber-600 dark:text-amber-400">
                Select at least {MIN_MODELS} models to compare
              </p>
            )}
          </div>
        </div>
      </div>

      {/* Generate Button */}
      <div className="flex justify-end">
        <Button
          size="lg"
          className="bg-amber-500 hover:bg-amber-600 text-white px-8"
          disabled={!canSubmit}
          onClick={handleGenerate}
        >
          {isGenerating ? (
            <>
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Generating Responses...
            </>
          ) : (
            <>
              <Sparkles className="mr-2 h-5 w-5" />
              Generate Comparison
            </>
          )}
        </Button>
      </div>

      {/* Info */}
      {isGenerating && (
        <div className="rounded-2xl border border-amber-200/60 bg-amber-50/50 p-4 text-center dark:border-amber-800/40 dark:bg-amber-900/20">
          <p className="text-sm text-amber-700 dark:text-amber-300">
            Generating responses from {selectedModels.length} models in parallel... This may take up
            to 30 seconds.
          </p>
        </div>
      )}
    </div>
  );
}
