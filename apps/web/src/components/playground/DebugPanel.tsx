'use client';

import { useState } from 'react';

import { Activity, BarChart3, ChevronDown, ChevronRight, Clock, Cpu, Database, DollarSign, Download, Gauge, Globe, Route, Search, Server, Terminal, Workflow } from 'lucide-react';

import { Button, Card, CardContent, CardHeader, CardTitle, Input, ScrollArea } from '@/components/ui';
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
  const { messages, tokenBreakdown, confidence, latencyMs, pipelineSteps, agentConfig, latencyBreakdown, costBreakdown, sessionTotalCost, activeStrategy, strategyInfo, pipelineTimings, cacheInfo, sessionCacheHits, sessionCacheRequests, apiTraces, logEntries } = usePlaygroundStore();
  const [expandedTrace, setExpandedTrace] = useState<number | null>(null);
  const [logLevelFilter, setLogLevelFilter] = useState<Set<string>>(new Set(['info', 'warn', 'error', 'debug']));
  const [logSourceFilter, setLogSourceFilter] = useState<string>('all');
  const [logSearch, setLogSearch] = useState('');

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

      {/* API Call Traces (Issue #4444) */}
      {apiTraces.length > 0 && (
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
                const formatBytes = (bytes: number) => {
                  if (bytes < 1024) return `${bytes}B`;
                  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`;
                  return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
                };

                return (
                  <div key={index} className="border rounded-md overflow-hidden">
                    <button
                      type="button"
                      className="w-full flex items-center gap-2 px-2 py-1.5 text-xs hover:bg-muted/50 transition-colors"
                      onClick={() => setExpandedTrace(isExpanded ? null : index)}
                    >
                      {isExpanded ? <ChevronDown className="h-3 w-3 shrink-0" /> : <ChevronRight className="h-3 w-3 shrink-0" />}
                      <span className={cn(
                        'w-2 h-2 rounded-full shrink-0',
                        trace.service === 'vector_search' && 'bg-blue-500',
                        trace.service === 'llm' && 'bg-purple-500',
                        trace.service === 'embedding' && 'bg-amber-500',
                        trace.service === 'reranker' && 'bg-green-500',
                      )} />
                      <span className="font-medium truncate flex-1 text-left">{trace.service}</span>
                      <span className={cn(
                        'font-mono text-[10px] px-1 py-0.5 rounded',
                        isError ? 'bg-red-100 text-red-800' : 'bg-green-100 text-green-800',
                      )}>{trace.statusCode}</span>
                      <span className="font-mono text-muted-foreground">{trace.latencyMs}ms</span>
                    </button>
                    {isExpanded && (
                      <div className="border-t px-2 py-1.5 bg-muted/30 space-y-1 text-[11px]">
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">URL</span>
                          <span className="font-mono truncate max-w-[180px]" title={trace.url}>{trace.url}</span>
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
                            <pre className="font-mono text-[10px] bg-background rounded p-1 max-h-20 overflow-auto whitespace-pre-wrap break-all">{trace.requestPreview}</pre>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
              {/* Legend */}
              <div className="flex gap-3 pt-1 text-[10px] text-muted-foreground">
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-blue-500" />search</span>
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-purple-500" />LLM</span>
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-amber-500" />embed</span>
                <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-green-500" />rerank</span>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Developer Console (Issue #4445) */}
      {logEntries.length > 0 && (() => {
        const sources = Array.from(new Set(logEntries.map(e => e.source)));
        const filtered = logEntries.filter(e => {
          if (!logLevelFilter.has(e.level)) return false;
          if (logSourceFilter !== 'all' && e.source !== logSourceFilter) return false;
          if (logSearch && !e.message.toLowerCase().includes(logSearch.toLowerCase())) return false;
          return true;
        });
        const levelCounts = { info: 0, warn: 0, error: 0, debug: 0 };
        for (const e of logEntries) {
          if (e.level in levelCounts) levelCounts[e.level as keyof typeof levelCounts]++;
        }

        const handleExportLogs = () => {
          const blob = new Blob([JSON.stringify(logEntries, null, 2)], { type: 'application/json' });
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `playground-logs-${Date.now()}.json`;
          a.click();
          URL.revokeObjectURL(url);
        };

        return (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm flex items-center gap-2">
                <Terminal className="h-4 w-4" />
                Developer Console
                <span className="text-[10px] text-muted-foreground font-normal ml-auto">
                  {filtered.length}/{logEntries.length}
                </span>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {/* Filters row */}
              <div className="flex items-center gap-1.5 flex-wrap">
                {/* Level toggles */}
                {(['info', 'warn', 'error', 'debug'] as const).map(level => (
                  <button
                    key={level}
                    type="button"
                    onClick={() => setLogLevelFilter(prev => {
                      const next = new Set(prev);
                      if (next.has(level)) next.delete(level); else next.add(level);
                      return next;
                    })}
                    className={cn(
                      'text-[10px] px-1.5 py-0.5 rounded border transition-colors',
                      logLevelFilter.has(level) ? {
                        info: 'bg-blue-100 text-blue-800 border-blue-200',
                        warn: 'bg-amber-100 text-amber-800 border-amber-200',
                        error: 'bg-red-100 text-red-800 border-red-200',
                        debug: 'bg-gray-100 text-gray-800 border-gray-200',
                      }[level] : 'bg-transparent text-muted-foreground border-muted opacity-50',
                    )}
                  >
                    {level} ({levelCounts[level]})
                  </button>
                ))}
                {/* Source dropdown */}
                <select
                  value={logSourceFilter}
                  onChange={e => setLogSourceFilter(e.target.value)}
                  className="text-[10px] h-5 px-1 border rounded bg-background"
                >
                  <option value="all">All sources</option>
                  {sources.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
                {/* Export */}
                <Button variant="ghost" size="icon" className="h-5 w-5 ml-auto" onClick={handleExportLogs} title="Export logs">
                  <Download className="h-3 w-3" />
                </Button>
              </div>
              {/* Search */}
              <div className="relative">
                <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-muted-foreground" />
                <Input
                  placeholder="Search logs..."
                  value={logSearch}
                  onChange={e => setLogSearch(e.target.value)}
                  className="h-6 text-[11px] pl-6 py-0"
                />
              </div>
              {/* Log entries */}
              <ScrollArea className="max-h-52">
                <div className="space-y-0.5 font-mono text-[11px]">
                  {filtered.length === 0 && (
                    <p className="text-xs text-muted-foreground italic py-2">No matching log entries</p>
                  )}
                  {filtered.map((entry, idx) => (
                    <div key={idx} className="flex items-start gap-1.5 px-1 py-0.5 hover:bg-muted/40 rounded">
                      <span className={cn(
                        'shrink-0 text-[9px] font-semibold uppercase w-10 text-right',
                        entry.level === 'info' && 'text-blue-600',
                        entry.level === 'warn' && 'text-amber-600',
                        entry.level === 'error' && 'text-red-600',
                        entry.level === 'debug' && 'text-gray-500',
                      )}>
                        {entry.level}
                      </span>
                      <span className={cn(
                        'shrink-0 text-[9px] px-1 rounded',
                        'bg-muted text-muted-foreground',
                      )}>
                        {entry.source}
                      </span>
                      <span className="flex-1 break-all">{entry.message}</span>
                    </div>
                  ))}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>
        );
      })()}

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
