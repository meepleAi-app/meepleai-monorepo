'use client';

import { Activity, BarChart3, Clock, Cpu, Gauge } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type { LatencyBreakdown, PipelineStepTiming } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';
import type { Message, PipelineStep } from '@/stores/playground-store';

interface MetricCardProps {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  value: string;
  subtitle: string;
}

function MetricCard({ icon: Icon, title, value, subtitle }: MetricCardProps) {
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

interface DebugPanelMetricsProps {
  messages: Message[];
  tokenBreakdown: { prompt: number; completion: number; total: number } | null;
  confidence: number | null;
  latencyMs: number | null;
  latencyBreakdown: LatencyBreakdown | null;
  pipelineTimings: PipelineStepTiming[];
  pipelineSteps: PipelineStep[];
}

export function DebugPanelMetrics({
  messages,
  tokenBreakdown,
  confidence,
  latencyMs,
  latencyBreakdown,
  pipelineTimings,
  pipelineSteps,
}: DebugPanelMetricsProps) {
  const totalTokens = messages.reduce((sum, msg) => sum + (msg.metadata?.tokens || 0), 0);
  const totalPipelineMs = pipelineTimings.reduce((sum, s) => sum + s.durationMs, 0);

  return (
    <>
      {/* Metrics Grid */}
      <div className="grid grid-cols-2 gap-3">
        <MetricCard
          icon={Cpu}
          title="Tokens"
          value={
            tokenBreakdown
              ? tokenBreakdown.total.toLocaleString()
              : totalTokens > 0
                ? totalTokens.toLocaleString()
                : '—'
          }
          subtitle={
            tokenBreakdown
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
                <span className="font-mono">
                  {(latencyBreakdown.retrievalMs / 1000).toFixed(2)}s
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Generation</span>
                <span className="font-mono">
                  {(latencyBreakdown.generationMs / 1000).toFixed(2)}s
                </span>
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
                        <span
                          className={cn(
                            'w-2 h-2 rounded-full shrink-0',
                            step.type === 'retrieval' && 'bg-blue-500',
                            step.type === 'compute' && 'bg-amber-500',
                            step.type === 'llm' && 'bg-purple-500'
                          )}
                        />
                        <span className="font-medium">{step.name}</span>
                      </span>
                      <span className="font-mono text-muted-foreground">
                        {step.durationMs}ms ({pct.toFixed(0)}%)
                      </span>
                    </div>
                    <div
                      className="relative h-3 rounded-full bg-muted overflow-hidden"
                      title={step.detail ?? undefined}
                    >
                      <div
                        className={cn(
                          'absolute inset-y-0 left-0 rounded-full transition-all',
                          step.type === 'retrieval' && 'bg-blue-400',
                          step.type === 'compute' && 'bg-amber-400',
                          step.type === 'llm' && 'bg-purple-400'
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
                <span className="flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-blue-500" />
                  retrieval
                </span>
                <span className="flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                  compute
                </span>
                <span className="flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-purple-500" />
                  LLM
                </span>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Pipeline Steps */}
      {pipelineSteps.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">Pipeline Steps</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-1.5 max-h-40 overflow-auto">
              {pipelineSteps.map((step, index) => {
                const duration =
                  index > 0 ? step.timestamp - pipelineSteps[index - 1].timestamp : 0;

                return (
                  <div key={index} className="flex items-center gap-2 text-xs">
                    <div
                      className={cn(
                        'w-2 h-2 rounded-full shrink-0',
                        index === pipelineSteps.length - 1
                          ? 'bg-green-500'
                          : 'bg-muted-foreground/40'
                      )}
                    />
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
          </CardContent>
        </Card>
      )}
    </>
  );
}
