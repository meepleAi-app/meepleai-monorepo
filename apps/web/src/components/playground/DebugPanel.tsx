'use client';

import { Activity, Clock, Cpu } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import { usePlaygroundStore } from '@/stores/playground-store';

export function DebugPanel() {
  const { messages } = usePlaygroundStore();

  // Calculate aggregate metrics
  const totalTokens = messages.reduce((sum, msg) => sum + (msg.metadata?.tokens || 0), 0);
  const avgLatency = messages
    .filter((msg) => msg.metadata?.latency)
    .reduce((sum, msg, _, arr) => sum + (msg.metadata!.latency! / arr.length), 0);
  const lastModel = messages.findLast((msg) => msg.metadata?.model)?.metadata?.model;

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Debug Information</h3>

      <div className="grid grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Cpu className="h-4 w-4" />
              Total Tokens
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalTokens}</div>
            <p className="text-xs text-muted-foreground">
              {messages.length} messages
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Clock className="h-4 w-4" />
              Avg Latency
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {avgLatency > 0 ? `${avgLatency.toFixed(2)}s` : '—'}
            </div>
            <p className="text-xs text-muted-foreground">Response time</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Activity className="h-4 w-4" />
              Model
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-lg font-bold">{lastModel || 'N/A'}</div>
            <p className="text-xs text-muted-foreground">Current model</p>
          </CardContent>
        </Card>
      </div>

      {/* Message Details */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">Message Details</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2 max-h-48 overflow-y-auto">
            {messages.length === 0 && (
              <p className="text-sm text-muted-foreground">No messages yet</p>
            )}
            {messages.slice(-5).reverse().map((msg) => (
              <div key={msg.id} className="text-xs space-y-1 pb-2 border-b last:border-0">
                <div className="font-medium">{msg.role}: {msg.content.slice(0, 50)}...</div>
                {msg.metadata && (
                  <div className="text-muted-foreground">
                    Tokens: {msg.metadata.tokens || 'N/A'} |
                    Latency: {msg.metadata.latency ? `${msg.metadata.latency}s` : 'N/A'}
                  </div>
                )}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
