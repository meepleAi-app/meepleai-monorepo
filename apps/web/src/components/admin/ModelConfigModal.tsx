/**
 * ModelConfigModal Component (Issue #2521)
 *
 * Modal for configuring AI model parameters with:
 * - Temperature slider (0-2)
 * - Max tokens input (512-8192)
 * - Test model with sample prompt
 * - Save/Cancel actions
 * - Real-time validation
 */

'use client';

import { useState, useEffect } from 'react';

import { Loader2, Check, TestTube2, Zap } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useUpdateModelConfig, useTestModel } from '@/hooks/queries';
import { DEFAULT_MODEL_CONFIG, type AiModelDto, type ConfigureModelRequest } from '@/lib/api';

interface ModelConfigModalProps {
  isOpen: boolean;
  onClose: () => void;
  modelId: string;
  model: AiModelDto | null;
}

export function ModelConfigModal({ isOpen, onClose, modelId, model }: ModelConfigModalProps) {
  // Form state
  const [temperature, setTemperature] = useState<number>(DEFAULT_MODEL_CONFIG.temperature);
  const [maxTokens, setMaxTokens] = useState<number>(DEFAULT_MODEL_CONFIG.maxTokens);
  const [maxTokensError, setMaxTokensError] = useState<string | null>(null);
  const [testPrompt, setTestPrompt] = useState('Explain quantum computing in one sentence.');
  const [testResult, setTestResult] = useState<{
    response: string;
    responseTime: number;
    cost: number;
  } | null>(null);

  // Mutations
  const updateMutation = useUpdateModelConfig();
  const testMutation = useTestModel();

  // Load current model config when modal opens
  useEffect(() => {
    if (isOpen && model) {
      setTemperature(model.temperature);
      setMaxTokens(model.maxTokens);
      setMaxTokensError(null);
      setTestResult(null);
    }
  }, [isOpen, model]);

  // Max tokens validation handler
  const handleMaxTokensChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Number(e.target.value);
    setMaxTokens(value);

    if (value < 512 || value > 8192) {
      setMaxTokensError('Max tokens must be between 512 and 8192');
    } else {
      setMaxTokensError(null);
    }
  };

  const handleSave = async () => {
    if (!model) return;

    const request: ConfigureModelRequest = {
      temperature,
      maxTokens,
    };

    try {
      await updateMutation.mutateAsync({ modelId, request });
      toast.success(`Configuration for "${model.displayName}" saved successfully!`);
      onClose();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to save model configuration'
      );
    }
  };

  const handleTestModel = async () => {
    if (!model) return;

    try {
      const result = await testMutation.mutateAsync({
        modelId,
        request: { modelId, testPrompt },
      });

      setTestResult({
        response: result.response,
        responseTime: result.responseTimeMs,
        cost: result.estimatedCost,
      });

      toast.success('Model test completed!');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to test model');
    }
  };

  const handleCancel = () => {
    setTestResult(null);
    onClose();
  };

  const isSaving = updateMutation.isPending;
  const isTesting = testMutation.isPending;

  if (!model) return null;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Configure: {model.displayName}</DialogTitle>
          <DialogDescription>
            Adjust model parameters for <span className="font-mono">{model.modelIdentifier}</span>
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6">
          {/* Temperature Slider */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="temperature" className="text-base font-semibold">
                Temperature
              </Label>
              <Badge variant="secondary">{temperature.toFixed(2)}</Badge>
            </div>
            <Slider
              id="temperature"
              min={0}
              max={2}
              step={0.1}
              value={[temperature]}
              onValueChange={([value]) => setTemperature(value)}
              className="w-full"
            />
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>0.0 (Precise)</span>
              <span>1.0 (Balanced)</span>
              <span>2.0 (Creative)</span>
            </div>
          </div>

          {/* Max Tokens Input */}
          <div className="space-y-2">
            <Label htmlFor="maxTokens" className="text-base font-semibold">
              Max Tokens
            </Label>
            <Input
              id="maxTokens"
              type="number"
              min={512}
              max={8192}
              step={256}
              value={maxTokens}
              onChange={handleMaxTokensChange}
              aria-invalid={!!maxTokensError}
              aria-describedby={maxTokensError ? 'maxTokens-error' : undefined}
              className={maxTokensError ? 'border-destructive' : ''}
            />
            {maxTokensError ? (
              <p id="maxTokens-error" className="text-sm text-destructive">
                {maxTokensError}
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">
                Maximum response length (512-8192 tokens)
              </p>
            )}
          </div>

          {/* Test Model Section */}
          <div className="space-y-2">
            <Label htmlFor="testPrompt" className="text-base font-semibold">
              Test Model
            </Label>
            <Textarea
              id="testPrompt"
              placeholder="Enter a test prompt..."
              value={testPrompt}
              onChange={(e) => setTestPrompt(e.target.value)}
              rows={3}
              maxLength={500}
            />
            <Button
              variant="outline"
              onClick={handleTestModel}
              disabled={isTesting || !testPrompt}
              className="w-full"
            >
              {isTesting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Testing...
                </>
              ) : (
                <>
                  <TestTube2 className="mr-2 h-4 w-4" />
                  Test Model
                </>
              )}
            </Button>

            {/* Test Result */}
            {testResult && (
              <Card className="bg-muted/50">
                <CardContent className="pt-4 space-y-2">
                  <div className="flex items-center justify-between">
                    <Badge variant="secondary">Response</Badge>
                    <div className="flex gap-2 text-xs text-muted-foreground">
                      <span>
                        <Zap className="inline h-3 w-3" /> {testResult.responseTime}ms
                      </span>
                      <span>💰 ${testResult.cost.toFixed(4)}</span>
                    </div>
                  </div>
                  <p className="text-sm">{testResult.response}</p>
                </CardContent>
              </Card>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleCancel} disabled={isSaving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={isSaving || !!maxTokensError}>
            {isSaving ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Saving...
              </>
            ) : (
              <>
                <Check className="mr-2 h-4 w-4" />
                Save Configuration
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
