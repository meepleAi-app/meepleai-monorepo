'use client';

/**
 * RAG Pipeline Builder - Template Selector
 *
 * Component for selecting and loading pre-built strategy templates.
 * Displays template cards with metrics and one-click loading.
 *
 * @see #3467 - Add strategy templates for quick start
 */

import { useState, useCallback, useMemo } from 'react';

import {
  Clock,
  Coins,
  FileText,
  Sparkles,
  Target,
  Zap,
  ChevronRight,
  Check,
  Lock,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import {
  ALL_TEMPLATES,
  getTemplatesForTier,
  cloneTemplatePipeline,
} from './strategy-templates';

import type { StrategyTemplate } from './strategy-templates';
import type { PipelineDefinition, UserTier } from './types';

// =============================================================================
// Types
// =============================================================================

export interface TemplateSelectorProps {
  /** Current user tier for access control */
  userTier: UserTier;
  /** Callback when a template is selected */
  onSelectTemplate: (pipeline: PipelineDefinition) => void;
  /** Currently selected template ID (for highlighting) */
  selectedTemplateId?: string;
  /** Layout mode */
  layout?: 'grid' | 'list';
  /** Additional class names */
  className?: string;
  /** Show as compact cards */
  compact?: boolean;
}

// =============================================================================
// Difficulty Badge
// =============================================================================

function DifficultyBadge({
  difficulty,
}: {
  difficulty: StrategyTemplate['difficulty'];
}) {
  const variants: Record<
    StrategyTemplate['difficulty'],
    { variant: 'default' | 'secondary' | 'destructive'; label: string }
  > = {
    beginner: { variant: 'default', label: 'Beginner' },
    intermediate: { variant: 'secondary', label: 'Intermediate' },
    advanced: { variant: 'destructive', label: 'Advanced' },
  };

  const { variant, label } = variants[difficulty];

  return (
    <Badge variant={variant} className="text-[10px]">
      {label}
    </Badge>
  );
}

// =============================================================================
// Template Card Component
// =============================================================================

interface TemplateCardProps {
  template: StrategyTemplate;
  isAvailable: boolean;
  isSelected: boolean;
  onSelect: () => void;
  onPreview: () => void;
  compact?: boolean;
}

function TemplateCard({
  template,
  isAvailable,
  isSelected,
  onSelect,
  onPreview,
  compact = false,
}: TemplateCardProps) {
  const formatCost = (cost: number) => {
    if (cost < 0.01) return `$${(cost * 1000).toFixed(1)}m`;
    return `$${cost.toFixed(3)}`;
  };

  const formatLatency = (ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  };

  const formatTokens = (tokens: number) => {
    if (tokens < 1000) return `${tokens}`;
    return `${(tokens / 1000).toFixed(1)}k`;
  };

  if (compact) {
    return (
      <Card
        className={cn(
          'cursor-pointer transition-all hover:shadow-md',
          isSelected && 'ring-2 ring-primary',
          !isAvailable && 'opacity-50'
        )}
        onClick={isAvailable ? onSelect : undefined}
      >
        <CardHeader className="p-3 pb-2">
          <div className="flex items-center gap-2">
            <span className="text-xl">{template.icon}</span>
            <div className="flex-1 min-w-0">
              <CardTitle className="text-sm truncate">{template.name}</CardTitle>
              <div className="flex items-center gap-1 mt-0.5">
                <DifficultyBadge difficulty={template.difficulty} />
                {!isAvailable && <Lock className="h-3 w-3 text-muted-foreground" />}
              </div>
            </div>
            {isSelected && <Check className="h-4 w-4 text-primary" />}
          </div>
        </CardHeader>
        <CardContent className="p-3 pt-0">
          <div className="flex items-center gap-3 text-[10px] text-muted-foreground">
            <span className="flex items-center gap-0.5">
              <Target className="h-3 w-3" />
              {template.accuracyScore}%
            </span>
            <span className="flex items-center gap-0.5">
              <Clock className="h-3 w-3" />
              {formatLatency(template.estimatedLatencyMs)}
            </span>
            <span className="flex items-center gap-0.5">
              <Coins className="h-3 w-3" />
              {formatCost(template.estimatedCost)}
            </span>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card
      className={cn(
        'transition-all hover:shadow-md',
        isSelected && 'ring-2 ring-primary',
        !isAvailable && 'opacity-60'
      )}
    >
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2">
            <span className="text-2xl">{template.icon}</span>
            <div>
              <CardTitle className="text-base">{template.name}</CardTitle>
              <div className="flex items-center gap-1.5 mt-1">
                <DifficultyBadge difficulty={template.difficulty} />
                <Badge variant="outline" className="text-[10px]">
                  {template.requiredTier}
                </Badge>
              </div>
            </div>
          </div>
          {!isAvailable && (
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger>
                  <Lock className="h-4 w-4 text-muted-foreground" />
                </TooltipTrigger>
                <TooltipContent>
                  Requires {template.requiredTier} tier
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          )}
        </div>
        <CardDescription className="text-xs mt-2">
          {template.description}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {/* Accuracy Score */}
        <div className="space-y-1">
          <div className="flex items-center justify-between text-xs">
            <span className="text-muted-foreground flex items-center gap-1">
              <Target className="h-3 w-3" />
              Accuracy Score
            </span>
            <span className="font-medium">{template.accuracyScore}%</span>
          </div>
          <Progress value={template.accuracyScore} className="h-1.5" />
        </div>

        {/* Metrics Grid */}
        <div className="grid grid-cols-3 gap-2 text-xs">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className="p-2 rounded bg-muted/50 text-center">
                  <Zap className="h-3 w-3 mx-auto mb-0.5 text-yellow-500" />
                  <div className="font-medium">
                    {formatLatency(template.estimatedLatencyMs)}
                  </div>
                  <div className="text-[10px] text-muted-foreground">Latency</div>
                </div>
              </TooltipTrigger>
              <TooltipContent>
                Estimated response time per query
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>

          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className="p-2 rounded bg-muted/50 text-center">
                  <FileText className="h-3 w-3 mx-auto mb-0.5 text-blue-500" />
                  <div className="font-medium">
                    {formatTokens(template.estimatedTokens)}
                  </div>
                  <div className="text-[10px] text-muted-foreground">Tokens</div>
                </div>
              </TooltipTrigger>
              <TooltipContent>
                Estimated token usage per query
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>

          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className="p-2 rounded bg-muted/50 text-center">
                  <Coins className="h-3 w-3 mx-auto mb-0.5 text-green-500" />
                  <div className="font-medium">
                    {formatCost(template.estimatedCost)}
                  </div>
                  <div className="text-[10px] text-muted-foreground">Cost</div>
                </div>
              </TooltipTrigger>
              <TooltipContent>
                Estimated cost per query (USD)
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>

        {/* Use Cases */}
        <div className="space-y-1">
          <span className="text-[10px] text-muted-foreground">Best for:</span>
          <div className="flex flex-wrap gap-1">
            {template.useCases.slice(0, 3).map((useCase) => (
              <Badge
                key={useCase}
                variant="outline"
                className="text-[10px] px-1.5"
              >
                {useCase}
              </Badge>
            ))}
            {template.useCases.length > 3 && (
              <Badge variant="outline" className="text-[10px] px-1.5">
                +{template.useCases.length - 3}
              </Badge>
            )}
          </div>
        </div>
      </CardContent>

      <CardFooter className="pt-0 gap-2">
        <Button
          variant="outline"
          size="sm"
          className="flex-1"
          onClick={onPreview}
        >
          Preview
        </Button>
        <Button
          size="sm"
          className="flex-1"
          disabled={!isAvailable}
          onClick={onSelect}
        >
          {isSelected ? (
            <>
              <Check className="h-3 w-3 mr-1" />
              Selected
            </>
          ) : (
            <>
              Use Template
              <ChevronRight className="h-3 w-3 ml-1" />
            </>
          )}
        </Button>
      </CardFooter>
    </Card>
  );
}

// =============================================================================
// Preview Dialog
// =============================================================================

interface PreviewDialogProps {
  template: StrategyTemplate | null;
  isOpen: boolean;
  onClose: () => void;
  onSelect: () => void;
  isAvailable: boolean;
}

function PreviewDialog({
  template,
  isOpen,
  onClose,
  onSelect,
  isAvailable,
}: PreviewDialogProps) {
  if (!template) return null;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="text-2xl">{template.icon}</span>
            {template.name}
          </DialogTitle>
          <DialogDescription>{template.description}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Pipeline Info */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <h4 className="text-sm font-medium">Pipeline Components</h4>
              <div className="text-sm text-muted-foreground">
                {template.pipeline.nodes.length} blocks,{' '}
                {template.pipeline.edges.length} connections
              </div>
              <div className="space-y-1">
                {template.pipeline.nodes.map((node) => (
                  <div
                    key={node.id}
                    className="flex items-center gap-2 text-xs"
                  >
                    <div className="w-2 h-2 rounded-full bg-primary" />
                    {node.data.block.name}
                  </div>
                ))}
              </div>
            </div>

            <div className="space-y-2">
              <h4 className="text-sm font-medium">Performance Metrics</h4>
              <div className="space-y-2">
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Accuracy Score</span>
                  <span className="font-medium">{template.accuracyScore}%</span>
                </div>
                <Progress value={template.accuracyScore} className="h-2" />

                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Latency</span>
                  <span>
                    {template.estimatedLatencyMs < 1000
                      ? `${template.estimatedLatencyMs}ms`
                      : `${(template.estimatedLatencyMs / 1000).toFixed(1)}s`}
                  </span>
                </div>

                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Tokens/Query</span>
                  <span>
                    {template.estimatedTokens < 1000
                      ? template.estimatedTokens
                      : `${(template.estimatedTokens / 1000).toFixed(1)}k`}
                  </span>
                </div>

                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Cost/Query</span>
                  <span>${template.estimatedCost.toFixed(4)}</span>
                </div>
              </div>
            </div>
          </div>

          {/* Use Cases */}
          <div className="space-y-2">
            <h4 className="text-sm font-medium">Best Use Cases</h4>
            <div className="flex flex-wrap gap-2">
              {template.useCases.map((useCase) => (
                <Badge key={useCase} variant="secondary">
                  {useCase}
                </Badge>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={onSelect} disabled={!isAvailable}>
            {isAvailable ? (
              <>
                <Sparkles className="h-4 w-4 mr-2" />
                Use This Template
              </>
            ) : (
              <>
                <Lock className="h-4 w-4 mr-2" />
                Requires {template.requiredTier}
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function TemplateSelector({
  userTier,
  onSelectTemplate,
  selectedTemplateId,
  layout = 'grid',
  className,
  compact = false,
}: TemplateSelectorProps) {
  const [previewTemplate, setPreviewTemplate] = useState<StrategyTemplate | null>(
    null
  );

  // Get available templates for user tier
  const availableTemplates = useMemo(
    () => getTemplatesForTier(userTier),
    [userTier]
  );

  const availableIds = useMemo(
    () => new Set(availableTemplates.map((t) => t.id)),
    [availableTemplates]
  );

  // Handle template selection
  const handleSelect = useCallback(
    (template: StrategyTemplate) => {
      const pipeline = cloneTemplatePipeline(template);
      onSelectTemplate(pipeline);
    },
    [onSelectTemplate]
  );

  // Handle preview
  const handlePreview = useCallback((template: StrategyTemplate) => {
    setPreviewTemplate(template);
  }, []);

  // Handle preview select
  const handlePreviewSelect = useCallback(() => {
    if (previewTemplate) {
      handleSelect(previewTemplate);
      setPreviewTemplate(null);
    }
  }, [previewTemplate, handleSelect]);

  return (
    <div className={className}>
      <div
        className={cn(
          layout === 'grid'
            ? 'grid gap-4 sm:grid-cols-2 lg:grid-cols-4'
            : 'space-y-3'
        )}
      >
        {ALL_TEMPLATES.map((template) => (
          <TemplateCard
            key={template.id}
            template={template}
            isAvailable={availableIds.has(template.id)}
            isSelected={selectedTemplateId === template.id}
            onSelect={() => handleSelect(template)}
            onPreview={() => handlePreview(template)}
            compact={compact}
          />
        ))}
      </div>

      {/* Preview Dialog */}
      <PreviewDialog
        template={previewTemplate}
        isOpen={!!previewTemplate}
        onClose={() => setPreviewTemplate(null)}
        onSelect={handlePreviewSelect}
        isAvailable={
          previewTemplate ? availableIds.has(previewTemplate.id) : false
        }
      />
    </div>
  );
}

export default TemplateSelector;
