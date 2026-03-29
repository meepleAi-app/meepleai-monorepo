'use client';

import { useState } from 'react';

import { ArrowDown, ChevronDown, ChevronRight, Layers, Workflow } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type { DataFlowStep, TomacLayer } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';

interface DebugPanelDataFlowProps {
  dataFlowSteps: DataFlowStep[];
  tomacLayers: TomacLayer[];
}

export function DebugPanelDataFlow({ dataFlowSteps, tomacLayers }: DebugPanelDataFlowProps) {
  const [expandedFlowStep, setExpandedFlowStep] = useState<number | null>(null);

  return (
    <>
      {/* Data Flow Visualization (Issue #4456) */}
      {dataFlowSteps.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Workflow className="h-4 w-4" />
              Data Flow
              <span className="text-[10px] text-muted-foreground font-normal ml-auto">
                {dataFlowSteps.length} steps
              </span>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-1">
              {dataFlowSteps.map((step, index) => {
                const isExpanded = expandedFlowStep === index;
                const stepColors: Record<string, string> = {
                  query: 'bg-blue-500',
                  config: 'bg-gray-500',
                  prompt: 'bg-purple-500',
                  search: 'bg-green-500',
                  context: 'bg-amber-500',
                  llm: 'bg-purple-500',
                  output: 'bg-emerald-500',
                };
                const dotColor = stepColors[step.stepType] ?? 'bg-gray-400';

                return (
                  <div key={index}>
                    {index > 0 && (
                      <div className="flex justify-center py-0.5">
                        <ArrowDown className="h-2.5 w-2.5 text-muted-foreground/40" />
                      </div>
                    )}
                    <button
                      type="button"
                      className="w-full flex items-center gap-2 px-2 py-1.5 rounded text-xs hover:bg-muted/50 transition-colors"
                      onClick={() => setExpandedFlowStep(isExpanded ? null : index)}
                    >
                      {isExpanded ? (
                        <ChevronDown className="h-3 w-3 shrink-0" />
                      ) : (
                        <ChevronRight className="h-3 w-3 shrink-0" />
                      )}
                      <span className={cn('w-2 h-2 rounded-full shrink-0', dotColor)} />
                      <span className="font-medium">{step.stepName}</span>
                      <span className="text-muted-foreground truncate flex-1 text-right">
                        {step.summary}
                      </span>
                    </button>
                    {isExpanded && (
                      <div className="ml-7 pl-2 border-l-2 border-muted space-y-1 pb-1">
                        {Object.entries(step.details).map(([key, value]) => (
                          <div key={key} className="flex justify-between text-[11px]">
                            <span className="text-muted-foreground">{key}</span>
                            <span
                              className="font-mono truncate max-w-[180px] text-right"
                              title={value}
                            >
                              {value}
                            </span>
                          </div>
                        ))}
                        {step.items && step.items.length > 0 && (
                          <div className="border-t pt-1 mt-1 space-y-0.5">
                            <span className="text-[10px] font-medium text-muted-foreground">
                              Items ({step.items.length})
                            </span>
                            {step.items.map((item, i) => (
                              <div
                                key={i}
                                className="text-[11px] space-y-0.5 pl-1 border-l border-muted-foreground/20 ml-1"
                              >
                                <div className="flex items-center gap-1.5">
                                  <span className="font-medium">{item.label}</span>
                                  {item.score !== null && (
                                    <span className="font-mono text-[10px] text-muted-foreground">
                                      score: {item.score.toFixed(3)}
                                    </span>
                                  )}
                                </div>
                                {item.preview && (
                                  <p
                                    className="text-muted-foreground text-[10px] truncate"
                                    title={item.preview}
                                  >
                                    {item.preview}
                                  </p>
                                )}
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}

      {/* TOMAC-RAG Layer Visualization (Issue #4446) */}
      {tomacLayers.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Layers className="h-4 w-4" />
              TOMAC-RAG Pipeline
              <span className="text-[10px] text-muted-foreground font-normal ml-auto">
                {tomacLayers.filter(l => l.status === 'active').length}/{tomacLayers.length} active
              </span>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-0">
              {tomacLayers.map((layer, index) => {
                const layerColors: Record<string, string> = {
                  L1: 'bg-blue-500',
                  L2: 'bg-purple-500',
                  L3: 'bg-green-500',
                  L4: 'bg-yellow-500',
                  L5: 'bg-orange-500',
                  L6: 'bg-red-500',
                };
                const dotColor = layerColors[layer.id] ?? 'bg-gray-400';

                return (
                  <div key={layer.id}>
                    {index > 0 && (
                      <div className="flex justify-center py-0.5">
                        <ArrowDown
                          className={cn(
                            'h-3 w-3',
                            layer.status === 'active' || tomacLayers[index - 1].status === 'active'
                              ? 'text-muted-foreground'
                              : 'text-muted-foreground/30'
                          )}
                        />
                      </div>
                    )}
                    <div
                      className={cn(
                        'flex items-center gap-2 px-2 py-1.5 rounded text-xs transition-colors',
                        layer.status === 'active' && 'bg-muted/60',
                        layer.status === 'planned' && 'opacity-50',
                        layer.status === 'bypassed' && 'opacity-60'
                      )}
                    >
                      <span className={cn('w-2.5 h-2.5 rounded-full shrink-0', dotColor)} />
                      <span className="font-mono text-[10px] text-muted-foreground w-5 shrink-0">
                        {layer.id}
                      </span>
                      <span className="font-medium flex-1 truncate" title={layer.name}>
                        {layer.name}
                      </span>
                      <span
                        className={cn(
                          'text-[9px] px-1.5 py-0.5 rounded-full leading-none font-semibold shrink-0',
                          layer.status === 'active' && 'bg-green-100 text-green-800',
                          layer.status === 'planned' && 'bg-gray-100 text-gray-600',
                          layer.status === 'bypassed' && 'bg-orange-100 text-orange-800'
                        )}
                      >
                        {layer.status}
                      </span>
                    </div>
                    {layer.status === 'active' && (
                      <div className="ml-9 pl-2 border-l-2 border-muted pb-1 space-y-0.5">
                        {layer.latencyMs > 0 && (
                          <div className="flex justify-between text-[11px] text-muted-foreground">
                            <span>Latency</span>
                            <span className="font-mono">{layer.latencyMs}ms</span>
                          </div>
                        )}
                        {layer.itemsProcessed > 0 && (
                          <div className="flex justify-between text-[11px] text-muted-foreground">
                            <span>Items</span>
                            <span className="font-mono">{layer.itemsProcessed}</span>
                          </div>
                        )}
                        {layer.score !== null && (
                          <div className="flex justify-between text-[11px] text-muted-foreground">
                            <span>Score</span>
                            <span className="font-mono">{(layer.score * 100).toFixed(0)}%</span>
                          </div>
                        )}
                        {layer.description && (
                          <p className="text-[10px] text-muted-foreground italic">
                            {layer.description}
                          </p>
                        )}
                      </div>
                    )}
                    {layer.status !== 'active' && layer.description && (
                      <p className="ml-9 text-[10px] text-muted-foreground/60 italic pb-0.5">
                        {layer.description}
                      </p>
                    )}
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}
    </>
  );
}
