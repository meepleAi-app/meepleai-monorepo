'use client';

import { Activity, Clock, Cpu, Gauge, Route, Server, Workflow } from 'lucide-react';

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
  const { messages, tokenBreakdown, confidence, latencyMs, pipelineSteps, agentConfig, latencyBreakdown, activeStrategy } = usePlaygroundStore();

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
              {/* Rough cost estimate at $0.003/1K tokens */}
              <div className="text-xs text-muted-foreground text-right">
                ~${((tokenBreakdown.total / 1000) * 0.003).toFixed(4)} est.
              </div>
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
                <span className="font-mono text-xs truncate max-w-[160px]" title={agentConfig.model}>{agentConfig.model}</span>
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

      {/* Active Strategy */}
      {activeStrategy && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Route className="h-4 w-4" />
              Strategy
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-sm font-mono">{activeStrategy}</div>
            <p className="text-xs text-muted-foreground mt-1">
              {activeStrategy === 'RetrievalOnly' && 'RAG chunks only, no LLM cost'}
              {activeStrategy === 'SingleModel' && 'RAG + single LLM call (POC)'}
              {activeStrategy === 'MultiModelConsensus' && 'RAG + dual-model consensus'}
            </p>
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
