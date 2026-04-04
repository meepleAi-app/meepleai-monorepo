'use client';

import { useState } from 'react';

import { ChevronDown, ChevronRight, Globe } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type { ApiTrace } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';

interface DebugPanelNetworkProps {
  apiTraces: ApiTrace[];
}

export function DebugPanelNetwork({ apiTraces }: DebugPanelNetworkProps) {
  const [expandedTrace, setExpandedTrace] = useState<number | null>(null);

  if (apiTraces.length === 0) return null;

  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes}B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
  };

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm flex items-center gap-2">
          <Globe className="h-4 w-4" />
          Network Traces
          <span className="text-[10px] text-muted-foreground font-normal ml-auto">
            {apiTraces.length} call{apiTraces.length !== 1 ? 's' : ''}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-1.5">
          {apiTraces.map((trace, index) => {
            const isExpanded = expandedTrace === index;
            const isError = trace.statusCode >= 400;

            return (
              <div key={index} className="border rounded-md overflow-hidden">
                <button
                  type="button"
                  className="w-full flex items-center gap-2 px-2 py-1.5 text-xs hover:bg-muted/50 transition-colors"
                  onClick={() => setExpandedTrace(isExpanded ? null : index)}
                >
                  {isExpanded ? (
                    <ChevronDown className="h-3 w-3 shrink-0" />
                  ) : (
                    <ChevronRight className="h-3 w-3 shrink-0" />
                  )}
                  <span
                    className={cn(
                      'w-2 h-2 rounded-full shrink-0',
                      trace.service === 'vector_search' && 'bg-blue-500',
                      trace.service === 'llm' && 'bg-purple-500',
                      trace.service === 'embedding' && 'bg-amber-500',
                      trace.service === 'reranker' && 'bg-green-500'
                    )}
                  />
                  <span className="font-medium truncate flex-1 text-left">{trace.service}</span>
                  <span
                    className={cn(
                      'font-mono text-[10px] px-1 py-0.5 rounded',
                      isError ? 'bg-red-100 text-red-800' : 'bg-green-100 text-green-800'
                    )}
                  >
                    {trace.statusCode}
                  </span>
                  <span className="font-mono text-muted-foreground">{trace.latencyMs}ms</span>
                </button>
                {isExpanded && (
                  <div className="border-t px-2 py-1.5 bg-muted/30 space-y-1 text-[11px]">
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">URL</span>
                      <span className="font-mono truncate max-w-[180px]" title={trace.url}>
                        {trace.url}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Request</span>
                      <span className="font-mono">{formatBytes(trace.requestSizeBytes)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Response</span>
                      <span className="font-mono">{formatBytes(trace.responseSizeBytes)}</span>
                    </div>
                    {trace.detail && (
                      <div className="border-t pt-1 mt-1">
                        <span className="text-muted-foreground">Detail: </span>
                        <span className="font-mono">{trace.detail}</span>
                      </div>
                    )}
                    {trace.requestPreview && (
                      <div className="border-t pt-1 mt-1">
                        <span className="text-muted-foreground block mb-0.5">Request preview:</span>
                        <pre className="font-mono text-[10px] bg-background rounded p-1 max-h-20 overflow-auto whitespace-pre-wrap break-all">
                          {trace.requestPreview}
                        </pre>
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
          {/* Legend */}
          <div className="flex gap-3 pt-1 text-[10px] text-muted-foreground">
            <span className="flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-blue-500" />
              search
            </span>
            <span className="flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-purple-500" />
              LLM
            </span>
            <span className="flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
              embed
            </span>
            <span className="flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
              rerank
            </span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
