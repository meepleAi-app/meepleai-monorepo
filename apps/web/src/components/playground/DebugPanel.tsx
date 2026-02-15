'use client';

import { Activity, BarChart3, Clock, Cpu, Database, DollarSign, Gauge, Route, Server, Workflow } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle, ScrollArea } from '@/components/ui';
import { cn } from '@/lib/utils';
import { usePlaygroundStore } from '@/stores/playground-store';

function MetricCard({
  icon: Icon,
  title,
  value,
  subtitle,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  value: string;
  subtitle: string;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm flex items-center gap-2">
          <Icon className="h-4 w-4" />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="text-xl font-bold">{value}</div>
        <p className="text-xs text-muted-foreground">{subtitle}</p>
      </CardContent>
    </Card>
  );
}

export function DebugPanel() {
  const { messages, tokenBreakdown, confidence, latencyMs, pipelineSteps, agentConfig, latencyBreakdown, costBreakdown, sessionTotalCost, activeStrategy, strategyInfo, pipelineTimings, cacheInfo, sessionCacheHits, sessionCacheRequests } = usePlaygroundStore();

  // Compute waterfall metrics
  const totalPipelineMs = pipelineTimings.reduce((sum, s) => sum + s.durationMs, 0);

  // Aggregate message-level tokens
  const totalTokens = messages.reduce((sum, msg) => sum + (msg.metadata?.tokens || 0), 0);

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Debug Information</h3>

      {/* Metrics Grid */}
      <div className="grid grid-cols-2 gap-3">
        <MetricCard
          icon={Cpu}
          title="Tokens"
          value={tokenBreakdown ? tokenBreakdown.total.toLocaleString() : totalTokens > 0 ? totalTokens.toLocaleString() : '—'}
          subtitle={tokenBreakdown
            ? `P: ${tokenBreakdown.prompt} | C: ${tokenBreakdown.completion}`
            : `${messages.length} messages`
          }
        />

        <MetricCard
          icon={Clock}
          title="Latency"
          value={latencyMs ? `${(latencyMs / 1000).toFixed(2)}s` : '—'}
          subtitle="Response time"
        />

        <MetricCard
          icon={Gauge}
          title="Confidence"
          value={confidence !== null ? `${(confidence * 100).toFixed(0)}%` : '—'}
          subtitle="Model confidence"
        />

        <MetricCard
          icon={Activity}
          title="Messages"
          value={String(messages.length)}
          subtitle={`${messages.filter(m => m.role === 'user').length} user, ${messages.filter(m => m.role === 'assistant').length} agent`}
        />
      </div>

      {/* Token Breakdown Detail */}
      {tokenBreakdown && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Token Breakdown</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Prompt</span>
                <span className="font-mono">{tokenBreakdown.prompt.toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Completion</span>
                <span className="font-mono">{tokenBreakdown.completion.toLocaleString()}</span>
              </div>
              <div className="border-t pt-2 flex justify-between text-sm font-medium">
                <span>Total</span>
                <span className="font-mono">{tokenBreakdown.total.toLocaleString()}</span>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Cost Breakdown */}
      {costBreakdown && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <DollarSign className="h-4 w-4" />
              Cost
              {costBreakdown.isFree && (
                <span className="text-[10px] bg-green-100 text-green-800 px-1 py-0.5 rounded leading-none">FREE</span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Input</span>
                <span className="font-mono">${costBreakdown.inputCost.toFixed(6)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Output</span>
                <span className="font-mono">${costBreakdown.outputCost.toFixed(6)}</span>
              </div>
              <div className="border-t pt-2 flex justify-between font-medium">
                <span>This request</span>
                <span className={cn(
                  'font-mono',
                  costBreakdown.isFree ? 'text-green-600' : costBreakdown.totalCost > 0.01 ? 'text-red-600' : 'text-amber-600'
                )}>
                  ${costBreakdown.totalCost.toFixed(6)}
                </span>
              </div>
              {sessionTotalCost > 0 && (
                <div className="border-t pt-2 flex justify-between text-xs text-muted-foreground">
                  <span>Session total</span>
                  <span className="font-mono">${sessionTotalCost.toFixed(6)}</span>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Cache Observability (Issue #4443) */}
      {cacheInfo && cacheInfo.status !== 'skip' && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Database className="h-4 w-4" />
              Cache
              <span className={cn(
                'text-[10px] px-1.5 py-0.5 rounded leading-none font-semibold',
                cacheInfo.status === 'hit' && 'bg-green-100 text-green-800',
                cacheInfo.status === 'miss' && 'bg-red-100 text-red-800',
              )}>{cacheInfo.status.toUpperCase()}</span>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              {cacheInfo.tier && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Tier</span>
                  <span className="font-mono">{cacheInfo.tier}</span>
                </div>
              )}
              {cacheInfo.cacheKey && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Key</span>
                  <span className="font-mono text-xs truncate max-w-[140px]" title={cacheInfo.cacheKey}>{cacheInfo.cacheKey}</span>
                </div>
              )}
              {cacheInfo.ttlSeconds > 0 && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">TTL</span>
                  <span className="font-mono">{cacheInfo.ttlSeconds}s</span>
                </div>
              )}
              {cacheInfo.latencyMs > 0 && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Lookup</span>
                  <span className="font-mono">{cacheInfo.latencyMs.toFixed(2)}ms</span>
                </div>
              )}
              {sessionCacheRequests > 0 && (
                <div className="border-t pt-2 flex justify-between text-xs text-muted-foreground">
                  <span>Session hit rate</span>
                  <span className="font-mono">
                    {sessionCacheHits}/{sessionCacheRequests} ({((sessionCacheHits / sessionCacheRequests) * 100).toFixed(0)}%)
                  </span>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Agent Config */}
      {agentConfig && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Server className="h-4 w-4" />
              Agent Config
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Provider</span>
                <span className="font-mono">{agentConfig.provider}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Model</span>
                <span className="flex items-center gap-1.5">
                  <span className="font-mono text-xs truncate max-w-[140px]" title={agentConfig.model}>{agentConfig.model}</span>
                  {agentConfig.isModelOverride && (
                    <span className="text-[10px] bg-amber-100 text-amber-800 px-1 py-0.5 rounded leading-none">override</span>
                  )}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Temperature</span>
                <span className="font-mono">{agentConfig.temperature}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Max Tokens</span>
                <span className="font-mono">{agentConfig.maxTokens.toLocaleString()}</span>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Strategy Info */}
      {(activeStrategy || strategyInfo) && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Route className="h-4 w-4" />
              Strategy
              {strategyInfo?.type && (
                <span className={cn(
                  'text-[10px] px-1.5 py-0.5 rounded leading-none',
                  strategyInfo.type === 'retrieval' && 'bg-blue-100 text-blue-800',
                  strategyInfo.type === 'generation' && 'bg-amber-100 text-amber-800',
                  strategyInfo.type === 'consensus' && 'bg-purple-100 text-purple-800',
                  strategyInfo.type === 'validation' && 'bg-green-100 text-green-800',
                  strategyInfo.type === 'custom' && 'bg-gray-100 text-gray-800',
                )}>{strategyInfo.type}</span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Execution</span>
                <span className="font-mono">{activeStrategy}</span>
              </div>
              {strategyInfo && (
                <>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Agent Strategy</span>
                    <span className="font-mono">{strategyInfo.name}</span>
                  </div>
                  {Object.keys(strategyInfo.parameters).length > 0 && (
                    <div className="border-t pt-2 space-y-1.5">
                      <span className="text-xs font-medium text-muted-foreground">Parameters</span>
                      {Object.entries(strategyInfo.parameters).map(([key, value]) => (
                        <div key={key} className="flex justify-between text-xs">
                          <span className="text-muted-foreground">{key}</span>
                          <span className="font-mono truncate max-w-[120px]" title={String(value)}>
                            {Array.isArray(value) ? value.join(', ') : String(value)}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              )}
              {!strategyInfo && (
                <p className="text-xs text-muted-foreground mt-1">
                  {activeStrategy === 'RetrievalOnly' && 'RAG chunks only, no LLM cost'}
                  {activeStrategy === 'SingleModel' && 'RAG + single LLM call (POC)'}
                  {activeStrategy === 'MultiModelConsensus' && 'RAG + dual-model consensus'}
                </p>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Latency Breakdown */}
      {latencyBreakdown && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Clock className="h-4 w-4" />
              Latency Breakdown
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Retrieval</span>
                <span className="font-mono">{(latencyBreakdown.retrievalMs / 1000).toFixed(2)}s</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Generation</span>
                <span className="font-mono">{(latencyBreakdown.generationMs / 1000).toFixed(2)}s</span>
              </div>
              <div className="border-t pt-2 flex justify-between font-medium">
                <span>Total</span>
                <span className="font-mono">{(latencyBreakdown.totalMs / 1000).toFixed(2)}s</span>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Pipeline Timing Waterfall */}
      {pipelineTimings.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <BarChart3 className="h-4 w-4" />
              Pipeline Waterfall
              <span className="text-[10px] text-muted-foreground font-normal ml-auto">
                {totalPipelineMs > 0 ? `${(totalPipelineMs / 1000).toFixed(2)}s total` : ''}
              </span>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {pipelineTimings.map((step, index) => {
                const pct = totalPipelineMs > 0 ? (step.durationMs / totalPipelineMs) * 100 : 0;
                return (
                  <div key={index} className="space-y-0.5">
                    <div className="flex items-center justify-between text-xs">
                      <span className="flex items-center gap-1.5">
                        <span className={cn(
                          'w-2 h-2 rounded-full shrink-0',
                          step.type === 'retrieval' && 'bg-blue-500',
                          step.type === 'compute' && 'bg-amber-500',
                          step.type === 'llm' && 'bg-purple-500',
                        )} />
                        <span className="font-medium">{step.name}</span>
                      </span>
                      <span className="font-mono text-muted-foreground">
                        {step.durationMs}ms ({pct.toFixed(0)}%)
                      </span>
                    </div>
                    <div className="relative h-3 rounded-full bg-muted overflow-hidden" title={step.detail ?? undefined}>
                      <div
                        className={cn(
                          'absolute inset-y-0 left-0 rounded-full transition-all',
                          step.type === 'retrieval' && 'bg-blue-400',
                          step.type === 'compute' && 'bg-amber-400',
                          step.type === 'llm' && 'bg-purple-400',
                        )}
                        style={{ width: `${Math.max(pct, 2)}%` }}
                      />
                      {step.detail && (
                        <span className="absolute inset-0 flex items-center px-1.5 text-[9px] text-muted-foreground truncate">
                          {step.detail}
                        </span>
                      )}
                    </div>
                  </div>
                );
              })}
              {/* Legend */}
              <div className="flex gap-3 pt-1 text-[10px] text-muted-foreground">
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-blue-500" />retrieval</span>
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-amber-500" />compute</span>
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-purple-500" />LLM</span>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Pipeline Steps */}
      {pipelineSteps.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Workflow className="h-4 w-4" />
              Pipeline Steps
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ScrollArea className="max-h-40">
              <div className="space-y-1.5">
                {pipelineSteps.map((step, index) => {
                  const duration = index > 0
                    ? step.timestamp - pipelineSteps[index - 1].timestamp
                    : 0;

                  return (
                    <div key={index} className="flex items-center gap-2 text-xs">
                      <div className={cn(
                        'w-2 h-2 rounded-full shrink-0',
                        index === pipelineSteps.length - 1
                          ? 'bg-green-500'
                          : 'bg-muted-foreground/40'
                      )} />
                      <span className="flex-1 truncate">{step.message}</span>
                      {duration > 0 && (
                        <span className="text-muted-foreground font-mono shrink-0">
                          +{(duration / 1000).toFixed(1)}s
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>
      )}

      {/* Message Details */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">Recent Messages</CardTitle>
        </CardHeader>
        <CardContent>
          <ScrollArea className="max-h-48">
            <div className="space-y-2">
              {messages.length === 0 && (
                <p className="text-sm text-muted-foreground">No messages yet</p>
              )}
              {messages.slice(-5).reverse().map((msg) => (
                <div key={msg.id} className="text-xs space-y-1 pb-2 border-b last:border-0">
                  <div className="font-medium">
                    {msg.role}: {msg.content.slice(0, 60)}{msg.content.length > 60 ? '...' : ''}
                  </div>
                  {msg.metadata && (
                    <div className="text-muted-foreground">
                      {msg.metadata.tokens ? `${msg.metadata.tokens} tok` : ''}
                      {msg.metadata.latency ? ` · ${msg.metadata.latency}ms` : ''}
                    </div>
                  )}
                  {msg.feedback && (
                    <div className="text-muted-foreground">
                      Feedback: {msg.feedback === 'up' ? '👍' : '👎'}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>
    </div>
  );
}
