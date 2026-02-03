/**
 * Test Metrics Display - Shows metrics for a single test result
 * Issue #3378
 */

'use client';

import { Clock, Coins, Gauge, Save, FileText } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';

interface TestResult {
  query: string;
  response: string;
  latency: number;
  tokensUsed: number;
  costEstimate: number;
  confidenceScore: number;
  citations: Array<{ page: number; text: string }>;
  timestamp: Date;
}

interface TestMetricsDisplayProps {
  result: TestResult;
  onSave?: () => void;
}

export function TestMetricsDisplay({ result, onSave }: TestMetricsDisplayProps) {
  const confidencePercent = Math.round(result.confidenceScore * 100);
  const confidenceColor =
    confidencePercent >= 85
      ? 'text-green-500'
      : confidencePercent >= 70
        ? 'text-yellow-500'
        : 'text-red-500';

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle>Latest Result Metrics</CardTitle>
            <CardDescription>
              {result.timestamp.toLocaleTimeString()}
            </CardDescription>
          </div>
          {onSave && (
            <Button variant="outline" size="sm" onClick={onSave}>
              <Save className="mr-2 h-4 w-4" />
              Save to History
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Metrics Grid */}
        <div className="grid grid-cols-2 gap-4">
          {/* Latency */}
          <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50">
            <Clock className="h-5 w-5 text-blue-500" />
            <div>
              <p className="text-xs text-muted-foreground">Latency</p>
              <p className="text-lg font-bold">{result.latency.toFixed(2)}s</p>
            </div>
          </div>

          {/* Tokens */}
          <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50">
            <Gauge className="h-5 w-5 text-purple-500" />
            <div>
              <p className="text-xs text-muted-foreground">Tokens Used</p>
              <p className="text-lg font-bold">{result.tokensUsed}</p>
            </div>
          </div>

          {/* Cost */}
          <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50">
            <Coins className="h-5 w-5 text-yellow-500" />
            <div>
              <p className="text-xs text-muted-foreground">Cost</p>
              <p className="text-lg font-bold">${result.costEstimate.toFixed(4)}</p>
            </div>
          </div>

          {/* Citations */}
          <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50">
            <FileText className="h-5 w-5 text-green-500" />
            <div>
              <p className="text-xs text-muted-foreground">Citations</p>
              <p className="text-lg font-bold">{result.citations.length}</p>
            </div>
          </div>
        </div>

        {/* Confidence Score */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium">Confidence Score</span>
            <span className={`text-lg font-bold ${confidenceColor}`}>
              {confidencePercent}%
            </span>
          </div>
          <Progress
            value={confidencePercent}
            className="h-2"
          />
          <p className="text-xs text-muted-foreground">
            {confidencePercent >= 85
              ? 'High confidence - reliable response'
              : confidencePercent >= 70
                ? 'Moderate confidence - review recommended'
                : 'Low confidence - response may be inaccurate'}
          </p>
        </div>

        {/* Citations List */}
        {result.citations.length > 0 && (
          <div className="space-y-2">
            <span className="text-sm font-medium">Citations</span>
            <div className="space-y-1">
              {result.citations.map((citation, idx) => (
                <div
                  key={idx}
                  className="flex items-center gap-2 p-2 rounded bg-muted/30 text-sm"
                >
                  <Badge variant="outline" className="shrink-0">
                    p.{citation.page}
                  </Badge>
                  <span className="text-muted-foreground truncate">
                    {citation.text}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
