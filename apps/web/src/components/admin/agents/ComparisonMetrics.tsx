/**
 * ComparisonMetrics Component
 * Issue #3380
 *
 * Displays comparative metrics across all tested configurations with winner highlighting.
 */

'use client';

import { Trophy, Clock, Coins, Target, TrendingUp, TrendingDown } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

interface Strategy {
  value: string;
  label: string;
  description: string;
  color: string;
}

interface Model {
  value: string;
  label: string;
  tier: string;
}

interface ComparisonResult {
  configId: string;
  strategy: string;
  model: string;
  response: string;
  latency: number;
  tokensUsed: number;
  costEstimate: number;
  confidenceScore: number;
  citations: Array<{ page: number; text: string }>;
  timestamp: Date;
}

export interface ComparisonMetricsProps {
  results: ComparisonResult[];
  strategies: readonly Strategy[];
  models: readonly Model[];
}

interface MetricWinner {
  configId: string;
  value: number;
  label: string;
  strategy: string;
}

const CONFIG_LABELS = ['A', 'B', 'C', 'D'];

export function ComparisonMetrics({ results, strategies, models }: ComparisonMetricsProps) {
  if (results.length === 0) return null;

  // Calculate winners for each metric
  const fastestResult = results.reduce((a, b) => (a.latency < b.latency ? a : b));
  const cheapestResult = results.reduce((a, b) => (a.costEstimate < b.costEstimate ? a : b));
  const mostConfidentResult = results.reduce((a, b) =>
    a.confidenceScore > b.confidenceScore ? a : b
  );
  const mostCitationsResult = results.reduce((a, b) =>
    a.citations.length > b.citations.length ? a : b
  );

  // Calculate overall winner (weighted score)
  const calculateScore = (result: ComparisonResult) => {
    const latencyScore = 1 - (result.latency / Math.max(...results.map(r => r.latency)));
    const costScore = 1 - (result.costEstimate / Math.max(...results.map(r => r.costEstimate)));
    const confidenceScore = result.confidenceScore;
    const citationScore = result.citations.length / Math.max(...results.map(r => r.citations.length), 1);

    // Weighted: Confidence 40%, Latency 25%, Cost 25%, Citations 10%
    return confidenceScore * 0.4 + latencyScore * 0.25 + costScore * 0.25 + citationScore * 0.1;
  };

  const resultScores = results.map(r => ({
    ...r,
    score: calculateScore(r),
  }));

  const overallWinner = resultScores.reduce((a, b) => (a.score > b.score ? a : b));

  const getConfigLabel = (configId: string) => {
    const idx = results.findIndex(r => r.configId === configId);
    return CONFIG_LABELS[idx] ?? '?';
  };

  const getStrategyLabel = (strategy: string) =>
    strategies.find(s => s.value === strategy)?.label ?? strategy;

  const getModelLabel = (model: string) =>
    models.find(m => m.value === model)?.label ?? model;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <TrendingUp className="h-5 w-5" />
          Comparison Summary
        </CardTitle>
        <CardDescription>
          Metrics comparison across {results.length} configurations
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
          {/* Overall Winner */}
          <div className="col-span-full lg:col-span-1 p-4 rounded-lg bg-gradient-to-br from-yellow-500/20 to-orange-500/20 border border-yellow-500/30">
            <div className="flex items-center gap-2 mb-2">
              <Trophy className="h-5 w-5 text-yellow-500" />
              <span className="font-semibold text-sm">Overall Winner</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge className="bg-yellow-500 text-yellow-950 font-bold text-lg px-3">
                {getConfigLabel(overallWinner.configId)}
              </Badge>
              <div>
                <p className="font-bold">{getStrategyLabel(overallWinner.strategy)}</p>
                <p className="text-xs text-muted-foreground">
                  {getModelLabel(overallWinner.model)}
                </p>
              </div>
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              Score: {(overallWinner.score * 100).toFixed(1)}%
            </p>
          </div>

          {/* Fastest */}
          <MetricCard
            icon={<Clock className="h-4 w-4 text-blue-500" />}
            title="Fastest"
            configLabel={getConfigLabel(fastestResult.configId)}
            strategy={getStrategyLabel(fastestResult.strategy)}
            value={`${fastestResult.latency.toFixed(2)}s`}
            isWinner={fastestResult.configId === overallWinner.configId}
          />

          {/* Cheapest */}
          <MetricCard
            icon={<Coins className="h-4 w-4 text-green-500" />}
            title="Cheapest"
            configLabel={getConfigLabel(cheapestResult.configId)}
            strategy={getStrategyLabel(cheapestResult.strategy)}
            value={`$${cheapestResult.costEstimate.toFixed(4)}`}
            isWinner={cheapestResult.configId === overallWinner.configId}
          />

          {/* Most Confident */}
          <MetricCard
            icon={<Target className="h-4 w-4 text-purple-500" />}
            title="Most Confident"
            configLabel={getConfigLabel(mostConfidentResult.configId)}
            strategy={getStrategyLabel(mostConfidentResult.strategy)}
            value={`${Math.round(mostConfidentResult.confidenceScore * 100)}%`}
            isWinner={mostConfidentResult.configId === overallWinner.configId}
          />

          {/* Most Citations */}
          <MetricCard
            icon={<TrendingDown className="h-4 w-4 text-orange-500" />}
            title="Most Sources"
            configLabel={getConfigLabel(mostCitationsResult.configId)}
            strategy={getStrategyLabel(mostCitationsResult.strategy)}
            value={`${mostCitationsResult.citations.length} citations`}
            isWinner={mostCitationsResult.configId === overallWinner.configId}
          />
        </div>

        {/* Detailed Comparison Table */}
        <div className="mt-6 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="text-left py-2 px-3 font-medium">Config</th>
                <th className="text-left py-2 px-3 font-medium">Strategy</th>
                <th className="text-left py-2 px-3 font-medium">Model</th>
                <th className="text-right py-2 px-3 font-medium">Latency</th>
                <th className="text-right py-2 px-3 font-medium">Cost</th>
                <th className="text-right py-2 px-3 font-medium">Confidence</th>
                <th className="text-right py-2 px-3 font-medium">Score</th>
              </tr>
            </thead>
            <tbody>
              {resultScores
                .sort((a, b) => b.score - a.score)
                .map((result, idx) => (
                  <tr
                    key={result.configId}
                    className={cn(
                      'border-b',
                      idx === 0 && 'bg-yellow-500/10'
                    )}
                  >
                    <td className="py-2 px-3">
                      <Badge
                        variant={idx === 0 ? 'default' : 'outline'}
                        className={cn(idx === 0 && 'bg-yellow-500 text-yellow-950')}
                      >
                        {getConfigLabel(result.configId)}
                      </Badge>
                    </td>
                    <td className="py-2 px-3">{getStrategyLabel(result.strategy)}</td>
                    <td className="py-2 px-3 text-muted-foreground">
                      {getModelLabel(result.model)}
                    </td>
                    <td className="py-2 px-3 text-right font-mono">
                      {result.latency.toFixed(2)}s
                      {result.configId === fastestResult.configId && (
                        <span className="ml-1 text-blue-500">🏆</span>
                      )}
                    </td>
                    <td className="py-2 px-3 text-right font-mono">
                      ${result.costEstimate.toFixed(4)}
                      {result.configId === cheapestResult.configId && (
                        <span className="ml-1 text-green-500">🏆</span>
                      )}
                    </td>
                    <td className="py-2 px-3 text-right font-mono">
                      {Math.round(result.confidenceScore * 100)}%
                      {result.configId === mostConfidentResult.configId && (
                        <span className="ml-1 text-purple-500">🏆</span>
                      )}
                    </td>
                    <td className="py-2 px-3 text-right font-bold">
                      {(result.score * 100).toFixed(1)}%
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}

interface MetricCardProps {
  icon: React.ReactNode;
  title: string;
  configLabel: string;
  strategy: string;
  value: string;
  isWinner: boolean;
}

function MetricCard({ icon, title, configLabel, strategy, value, isWinner }: MetricCardProps) {
  return (
    <div
      className={cn(
        'p-3 rounded-lg border',
        isWinner ? 'border-yellow-500/30 bg-yellow-500/5' : 'bg-muted/30'
      )}
    >
      <div className="flex items-center gap-2 mb-2">
        {icon}
        <span className="text-xs font-medium text-muted-foreground">{title}</span>
      </div>
      <div className="flex items-center gap-2">
        <Badge variant="outline" className="font-bold">
          {configLabel}
        </Badge>
        <span className="text-sm font-medium truncate">{strategy}</span>
      </div>
      <p className="text-lg font-bold mt-1">{value}</p>
    </div>
  );
}
