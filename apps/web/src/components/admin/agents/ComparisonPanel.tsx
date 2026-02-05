/**
 * ComparisonPanel Component
 * Issue #3380
 *
 * Displays a single comparison result with response and metrics.
 */

'use client';

import { Trophy, Clock, Coins, Gauge, FileText } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
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

export interface ComparisonPanelProps {
  result: ComparisonResult;
  strategies: readonly Strategy[];
  models: readonly Model[];
  isWinner?: boolean;
}

const STRATEGY_COLORS: Record<string, string> = {
  FAST: 'border-green-500',
  BALANCED: 'border-blue-500',
  PRECISE: 'border-purple-500',
  EXPERT: 'border-orange-500',
  CONSENSUS: 'border-red-500',
};

export function ComparisonPanel({
  result,
  strategies,
  models,
  isWinner = false,
}: ComparisonPanelProps) {
  const strategy = strategies.find(s => s.value === result.strategy);
  const model = models.find(m => m.value === result.model);
  const confidencePercent = Math.round(result.confidenceScore * 100);

  const confidenceColor =
    confidencePercent >= 85
      ? 'text-green-500'
      : confidencePercent >= 70
        ? 'text-yellow-500'
        : 'text-red-500';

  return (
    <Card
      className={cn(
        'border-2 transition-all',
        STRATEGY_COLORS[result.strategy] ?? 'border-muted',
        isWinner && 'ring-2 ring-yellow-400 shadow-lg shadow-yellow-400/20'
      )}
    >
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <CardTitle className="text-base">{strategy?.label ?? result.strategy}</CardTitle>
              {isWinner && (
                <Badge className="bg-yellow-500 text-yellow-950 gap-1">
                  <Trophy className="h-3 w-3" />
                  Winner
                </Badge>
              )}
            </div>
            <p className="text-xs text-muted-foreground">
              {model?.label ?? result.model}
            </p>
          </div>
          <Badge variant="outline" className="text-xs">
            {model?.tier ?? 'unknown'}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Response */}
        <div className="space-y-2">
          <label className="text-xs font-medium text-muted-foreground">Response</label>
          <ScrollArea className="h-32 rounded-md border p-3 bg-muted/30">
            <p className="text-sm leading-relaxed">{result.response}</p>
          </ScrollArea>
        </div>

        {/* Metrics Grid */}
        <div className="grid grid-cols-2 gap-2">
          <div className="flex items-center gap-2 p-2 rounded bg-muted/50">
            <Clock className="h-4 w-4 text-blue-500" />
            <div>
              <p className="text-xs text-muted-foreground">Latency</p>
              <p className="text-sm font-bold">{result.latency.toFixed(2)}s</p>
            </div>
          </div>

          <div className="flex items-center gap-2 p-2 rounded bg-muted/50">
            <Coins className="h-4 w-4 text-yellow-500" />
            <div>
              <p className="text-xs text-muted-foreground">Cost</p>
              <p className="text-sm font-bold">${result.costEstimate.toFixed(4)}</p>
            </div>
          </div>

          <div className="flex items-center gap-2 p-2 rounded bg-muted/50">
            <Gauge className="h-4 w-4 text-purple-500" />
            <div>
              <p className="text-xs text-muted-foreground">Tokens</p>
              <p className="text-sm font-bold">{result.tokensUsed}</p>
            </div>
          </div>

          <div className="flex items-center gap-2 p-2 rounded bg-muted/50">
            <FileText className="h-4 w-4 text-green-500" />
            <div>
              <p className="text-xs text-muted-foreground">Citations</p>
              <p className="text-sm font-bold">{result.citations.length}</p>
            </div>
          </div>
        </div>

        {/* Confidence Score */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium">Confidence</span>
            <span className={cn('text-sm font-bold', confidenceColor)}>
              {confidencePercent}%
            </span>
          </div>
          <Progress value={confidencePercent} className="h-2" />
        </div>

        {/* Citations Preview */}
        {result.citations.length > 0 && (
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">Sources</label>
            <div className="flex flex-wrap gap-1">
              {result.citations.slice(0, 3).map((citation, idx) => (
                <Badge key={idx} variant="secondary" className="text-xs">
                  p.{citation.page}
                </Badge>
              ))}
              {result.citations.length > 3 && (
                <Badge variant="outline" className="text-xs">
                  +{result.citations.length - 3} more
                </Badge>
              )}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
