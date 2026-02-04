/**
 * Test Chat Interface - Chat history display for agent testing
 * Issue #3378
 */

'use client';

import { User, Bot, Save, Clock, Gauge, Coins, Target } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';

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

interface TestChatInterfaceProps {
  results: TestResult[];
  onSave: (result: TestResult) => void;
}

export function TestChatInterface({ results, onSave }: TestChatInterfaceProps) {
  if (results.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Test History</CardTitle>
          <CardDescription>Your test queries and responses will appear here</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <Bot className="h-12 w-12 text-muted-foreground/50 mb-4" />
            <p className="text-muted-foreground">
              No tests yet. Send a query to get started.
            </p>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Test History</CardTitle>
        <CardDescription>
          {results.length} test{results.length !== 1 ? 's' : ''} in this session
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ScrollArea className="h-[500px] pr-4">
          <div className="space-y-6">
            {results.map((result, idx) => (
              <div key={idx} className="space-y-4 pb-6 border-b last:border-0">
                {/* User Query */}
                <div className="flex gap-3">
                  <div className="flex-shrink-0 w-8 h-8 rounded-full bg-blue-500/20 flex items-center justify-center">
                    <User className="h-4 w-4 text-blue-500" />
                  </div>
                  <div className="flex-1">
                    <p className="text-sm font-medium mb-1">You</p>
                    <p className="text-sm text-muted-foreground">{result.query}</p>
                  </div>
                </div>

                {/* Agent Response */}
                <div className="flex gap-3">
                  <div className="flex-shrink-0 w-8 h-8 rounded-full bg-purple-500/20 flex items-center justify-center">
                    <Bot className="h-4 w-4 text-purple-500" />
                  </div>
                  <div className="flex-1">
                    <p className="text-sm font-medium mb-1">Agent</p>
                    <p className="text-sm text-muted-foreground mb-3">{result.response}</p>

                    {/* Inline Metrics */}
                    <div className="flex flex-wrap gap-2 mb-3">
                      <Badge variant="outline" className="text-xs">
                        <Clock className="h-3 w-3 mr-1" />
                        {result.latency.toFixed(2)}s
                      </Badge>
                      <Badge variant="outline" className="text-xs">
                        <Gauge className="h-3 w-3 mr-1" />
                        {result.tokensUsed} tokens
                      </Badge>
                      <Badge variant="outline" className="text-xs">
                        <Coins className="h-3 w-3 mr-1" />
                        ${result.costEstimate.toFixed(4)}
                      </Badge>
                      <Badge
                        variant={result.confidenceScore >= 0.85 ? 'default' : 'secondary'}
                        className="text-xs"
                      >
                        <Target className="h-3 w-3 mr-1" />
                        {Math.round(result.confidenceScore * 100)}%
                      </Badge>
                    </div>

                    {/* Citations */}
                    {result.citations.length > 0 && (
                      <div className="flex flex-wrap gap-1 mb-3">
                        {result.citations.map((citation, citIdx) => (
                          <Badge
                            key={citIdx}
                            variant="outline"
                            className="text-xs bg-green-500/10 text-green-600"
                          >
                            p.{citation.page}
                          </Badge>
                        ))}
                      </div>
                    )}

                    {/* Actions */}
                    <div className="flex items-center gap-2">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onSave(result)}
                        className="text-xs"
                      >
                        <Save className="h-3 w-3 mr-1" />
                        Save
                      </Button>
                      <span className="text-xs text-muted-foreground">
                        {result.timestamp.toLocaleTimeString()}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
