'use client';

import React from 'react';

import { Layers, SplitSquareVertical, ListOrdered, Target } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';
import { cn } from '@/lib/utils';

import type { RetrievalParamsProps } from './types';

/**
 * RetrievalParams Component
 *
 * Controls for document retrieval parameters:
 * - Chunk Size (200-2000 tokens)
 * - Chunk Overlap (0-50%)
 * - Top Results (1-20)
 * - Similarity Threshold (0-1)
 */
export function RetrievalParams({
  params,
  onChange,
  className,
}: RetrievalParamsProps): React.JSX.Element {
  const handleChunkSizeChange = (value: number[]) => {
    onChange({ ...params, chunkSize: value[0] });
  };

  const handleChunkOverlapChange = (value: number[]) => {
    onChange({ ...params, chunkOverlap: value[0] });
  };

  const handleTopResultsChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(1, Math.min(20, parseInt(e.target.value) || 1));
    onChange({ ...params, topResults: value });
  };

  const handleSimilarityChange = (value: number[]) => {
    onChange({ ...params, similarityThreshold: value[0] });
  };

  return (
    <div className={cn('space-y-6', className)}>
      {/* Chunk Size */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Layers className="h-4 w-4 text-blue-500" />
                  Chunk Size
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Size of text chunks in tokens. Smaller chunks (200-400) provide more precise
                  retrieval; larger chunks (800-2000) preserve more context.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <span className="text-sm font-mono text-muted-foreground">
            {params.chunkSize} tokens
          </span>
        </div>
        <Slider
          value={[params.chunkSize]}
          onValueChange={handleChunkSizeChange}
          min={200}
          max={2000}
          step={50}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>Precise</span>
          <span>Contextual</span>
        </div>
      </div>

      {/* Chunk Overlap */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <SplitSquareVertical className="h-4 w-4 text-purple-500" />
                  Chunk Overlap
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Percentage of overlap between consecutive chunks. Higher overlap (20-50%)
                  ensures no information is lost at chunk boundaries.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <span className="text-sm font-mono text-muted-foreground">
            {params.chunkOverlap}%
          </span>
        </div>
        <Slider
          value={[params.chunkOverlap]}
          onValueChange={handleChunkOverlapChange}
          min={0}
          max={50}
          step={5}
          className="w-full"
        />
      </div>

      {/* Top Results */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <ListOrdered className="h-4 w-4 text-green-500" />
                  Top Results
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Number of most relevant chunks to retrieve. More results provide broader
                  context but increase latency and token usage.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <Input
            type="number"
            value={params.topResults}
            onChange={handleTopResultsChange}
            min={1}
            max={20}
            className="w-16 h-8 text-right font-mono"
          />
        </div>
      </div>

      {/* Similarity Threshold */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Target className="h-4 w-4 text-orange-500" />
                  Similarity Threshold
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Minimum cosine similarity score for retrieved chunks. Higher values (0.8+)
                  return only highly relevant content; lower values (0.5-0.7) cast a wider net.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <span className="text-sm font-mono text-muted-foreground">
            {params.similarityThreshold.toFixed(2)}
          </span>
        </div>
        <Slider
          value={[params.similarityThreshold]}
          onValueChange={handleSimilarityChange}
          min={0}
          max={1}
          step={0.05}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>Broader</span>
          <span>Stricter</span>
        </div>
      </div>
    </div>
  );
}
