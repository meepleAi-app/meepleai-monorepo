'use client';

/**
 * RAG Pipeline Builder - Test Panel Component
 *
 * Panel for testing RAG pipelines with real-time SSE streaming results.
 * Shows progress, metrics, retrieved documents, and validation results.
 *
 * @see #3463 - Live test API with SSE streaming
 */

import { useState, useCallback, useMemo } from 'react';

import {
  Play,
  Square,
  RotateCcw,
  ChevronDown,
  ChevronRight,
  CheckCircle,
  XCircle,
  Clock,
  Coins,
  FileText,
  AlertCircle,
  Loader2,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
import { Progress } from '@/components/ui/feedback/progress';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import {
  useRagPipelineTest,
  type BlockResult,
  type RetrievedDocument,
} from '@/lib/domain-hooks/useRagPipelineTest';
import { cn } from '@/lib/utils';

import type { PipelineDefinition } from './types';

// =============================================================================
// Types
// =============================================================================

export interface PipelineTestPanelProps {
  /** Current pipeline definition to test */
  pipeline: PipelineDefinition;
  /** Additional class names */
  className?: string;
  /** Whether panel is disabled */
  disabled?: boolean;
}

// =============================================================================
// Sub-Components
// =============================================================================

interface BlockResultItemProps {
  result: BlockResult;
  isExpanded: boolean;
  onToggle: () => void;
}

function BlockResultItem({ result, isExpanded, onToggle }: BlockResultItemProps) {
  return (
    <Collapsible open={isExpanded} onOpenChange={onToggle}>
      <CollapsibleTrigger className="flex items-center gap-2 w-full p-2 hover:bg-accent/50 rounded-md transition-colors">
        {isExpanded ? (
          <ChevronDown className="h-3 w-3 text-muted-foreground" />
        ) : (
          <ChevronRight className="h-3 w-3 text-muted-foreground" />
        )}
        {result.success ? (
          <CheckCircle className="h-4 w-4 text-green-500" />
        ) : (
          <XCircle className="h-4 w-4 text-red-500" />
        )}
        <span className="text-sm font-medium flex-1 text-left truncate">{result.blockName}</span>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>{result.durationMs}ms</span>
          <span>{result.tokensUsed} tok</span>
        </div>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <div className="pl-7 pr-2 pb-2 space-y-2">
          {/* Block output */}
          {result.output && (
            <div className="p-2 bg-muted/50 rounded-md">
              <p className="text-xs text-muted-foreground mb-1">Output:</p>
              <p className="text-xs">{result.output}</p>
            </div>
          )}

          {/* Error */}
          {result.error && (
            <div className="p-2 bg-red-500/10 border border-red-500/20 rounded-md">
              <p className="text-xs text-red-500">{result.error}</p>
            </div>
          )}

          {/* Retrieved documents */}
          {result.documents && result.documents.length > 0 && (
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground flex items-center gap-1">
                <FileText className="h-3 w-3" />
                {result.documents.length} documents retrieved
              </p>
              <div className="space-y-1">
                {result.documents.slice(0, 3).map(doc => (
                  <DocumentPreview key={doc.id} document={doc} />
                ))}
                {result.documents.length > 3 && (
                  <p className="text-xs text-muted-foreground pl-2">
                    +{result.documents.length - 3} more...
                  </p>
                )}
              </div>
            </div>
          )}

          {/* Validation result */}
          {result.validation && (
            <div
              className={cn(
                'p-2 rounded-md border',
                result.validation.passed
                  ? 'bg-green-500/10 border-green-500/20'
                  : 'bg-yellow-500/10 border-yellow-500/20'
              )}
            >
              <div className="flex items-center justify-between">
                <span className="text-xs font-medium">{result.validation.type}</span>
                <Badge
                  variant={result.validation.passed ? 'secondary' : 'outline'}
                  className="text-xs"
                >
                  {(result.validation.score * 100).toFixed(0)}%
                </Badge>
              </div>
              {result.validation.details && (
                <p className="text-xs text-muted-foreground mt-1">{result.validation.details}</p>
              )}
            </div>
          )}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

interface DocumentPreviewProps {
  document: RetrievedDocument;
}

function DocumentPreview({ document }: DocumentPreviewProps) {
  return (
    <div className="p-2 bg-muted/30 rounded-md border">
      <div className="flex items-center justify-between mb-1">
        <span className="text-xs font-medium truncate">{document.title}</span>
        <Badge variant="outline" className="text-[10px]">
          {(document.score * 100).toFixed(0)}%
        </Badge>
      </div>
      <p className="text-xs text-muted-foreground line-clamp-2">{document.content}</p>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function PipelineTestPanel({
  pipeline,
  className,
  disabled = false,
}: PipelineTestPanelProps) {
  const [testQuery, setTestQuery] = useState('How do I setup the game?');
  const [expandedBlocks, setExpandedBlocks] = useState<Set<string>>(new Set());

  const {
    isRunning,
    progress,
    metrics,
    blockResults,
    finalResponse,
    error,
    startTest,
    cancelTest,
    reset,
  } = useRagPipelineTest({
    onComplete: () => {
      // Auto-expand all blocks on completion
      setExpandedBlocks(new Set(Array.from(blockResults.keys())));
    },
  });

  const handleStartTest = useCallback(() => {
    if (!testQuery.trim()) return;
    startTest(pipeline, testQuery);
  }, [pipeline, testQuery, startTest]);

  const handleToggleBlock = useCallback((blockId: string) => {
    setExpandedBlocks(prev => {
      const next = new Set(prev);
      if (next.has(blockId)) {
        next.delete(blockId);
      } else {
        next.add(blockId);
      }
      return next;
    });
  }, []);

  // Sort block results by execution order
  const sortedBlockResults = useMemo(() => {
    return Array.from(blockResults.values());
  }, [blockResults]);

  const canRunTest = !disabled && !isRunning && testQuery.trim().length > 0;

  return (
    <div className={cn('flex flex-col h-full bg-card', className)} data-testid="test-panel">
      {/* Header */}
      <div className="p-3 border-b space-y-3">
        <h3 className="font-semibold text-sm">Pipeline Test</h3>

        {/* Query input */}
        <div className="space-y-2">
          <Input
            placeholder="Enter test query..."
            value={testQuery}
            onChange={e => setTestQuery(e.target.value)}
            disabled={isRunning || disabled}
            className="h-8 text-sm"
            data-testid="test-query-input"
            onKeyDown={e => {
              if (e.key === 'Enter' && canRunTest) {
                handleStartTest();
              }
            }}
          />

          {/* Action buttons */}
          <div className="flex items-center gap-2">
            {!isRunning ? (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button
                      size="sm"
                      onClick={handleStartTest}
                      disabled={!canRunTest}
                      className="flex-1"
                      data-testid="run-test-button"
                    >
                      <Play className="h-3 w-3 mr-1" />
                      Run Test
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>
                    {pipeline.nodes.length === 0
                      ? 'Add blocks to test'
                      : 'Test pipeline with sample query'}
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            ) : (
              <Button size="sm" variant="destructive" onClick={cancelTest} className="flex-1">
                <Square className="h-3 w-3 mr-1" />
                Cancel
              </Button>
            )}

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    size="icon"
                    variant="outline"
                    onClick={reset}
                    disabled={isRunning}
                    className="h-8 w-8"
                  >
                    <RotateCcw className="h-3 w-3" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Reset results</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
        </div>
      </div>

      {/* Progress */}
      {(isRunning || progress.totalBlocks > 0) && (
        <div className="p-3 border-b space-y-2">
          <div className="flex items-center justify-between text-xs">
            <span className="text-muted-foreground">
              {isRunning ? (
                <span className="flex items-center gap-1">
                  <Loader2 className="h-3 w-3 animate-spin" />
                  Testing...
                </span>
              ) : (
                'Completed'
              )}
            </span>
            <span>
              {progress.completedBlocks}/{progress.totalBlocks} blocks
            </span>
          </div>
          <Progress value={progress.percentage} className="h-2" />

          {progress.currentBlock && isRunning && (
            <p className="text-xs text-muted-foreground">Running: {progress.currentBlock.name}</p>
          )}
        </div>
      )}

      {/* Metrics */}
      {metrics.blocksExecuted > 0 && (
        <div className="p-3 border-b">
          <div className="grid grid-cols-2 gap-2">
            <div className="flex items-center gap-2 text-xs">
              <Clock className="h-3 w-3 text-muted-foreground" />
              <span>{metrics.totalDurationMs}ms</span>
            </div>
            <div className="flex items-center gap-2 text-xs">
              <FileText className="h-3 w-3 text-muted-foreground" />
              <span>{metrics.totalTokensUsed} tokens</span>
            </div>
            <div className="flex items-center gap-2 text-xs">
              <Coins className="h-3 w-3 text-muted-foreground" />
              <span>${metrics.totalCost.toFixed(4)}</span>
            </div>
            <div className="flex items-center gap-2 text-xs">
              {metrics.blocksFailed === 0 ? (
                <CheckCircle className="h-3 w-3 text-green-500" />
              ) : (
                <AlertCircle className="h-3 w-3 text-red-500" />
              )}
              <span>
                {metrics.blocksExecuted - metrics.blocksFailed}/{metrics.blocksExecuted} passed
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Block Results */}
      <ScrollArea className="flex-1">
        <div className="p-2 space-y-1">
          {sortedBlockResults.length > 0 ? (
            sortedBlockResults.map(result => (
              <BlockResultItem
                key={result.blockId}
                result={result}
                isExpanded={expandedBlocks.has(result.blockId)}
                onToggle={() => handleToggleBlock(result.blockId)}
              />
            ))
          ) : !isRunning ? (
            <div className="text-center py-8 text-muted-foreground">
              <Play className="h-8 w-8 mx-auto mb-2 opacity-50" />
              <p className="text-sm">Run a test to see results</p>
              <p className="text-xs mt-1">{pipeline.nodes.length} blocks in pipeline</p>
            </div>
          ) : null}
        </div>
      </ScrollArea>

      {/* Final Response */}
      {finalResponse && (
        <div className="p-3 border-t">
          <p className="text-xs font-medium mb-1">Generated Response:</p>
          <div className="p-2 bg-muted/50 rounded-md">
            <p className="text-xs">{finalResponse}</p>
          </div>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="p-3 border-t">
          <div className="p-2 bg-red-500/10 border border-red-500/20 rounded-md">
            <p className="text-xs text-red-500">{error.message}</p>
          </div>
        </div>
      )}
    </div>
  );
}

export default PipelineTestPanel;
