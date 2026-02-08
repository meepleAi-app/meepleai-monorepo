'use client';

import { Badge, Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui';
import { Activity, Zap, TrendingUp, Users } from 'lucide-react';
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

const mockAgents = [
  {
    agentName: 'qa-agent',
    displayName: 'Q&A Agent',
    description: 'Answer questions about board games',
    modelProvider: 'OpenRouter',
    modelName: 'deepseek/deepseek-chat',
    isActive: true,
    executionCount: 1247,
    totalTokens: 456789,
    inputTokens: 234567,
    outputTokens: 222222,
    averageLatencyMs: 1250,
    successRate: 0.98,
    lastExecutedAt: new Date('2026-02-08T00:30:00Z'),
    tokensOverTime: [
      { date: '2026-02-04', inputTokens: 30000, outputTokens: 28000, totalTokens: 58000 },
      { date: '2026-02-05', inputTokens: 32000, outputTokens: 30000, totalTokens: 62000 },
      { date: '2026-02-06', inputTokens: 35000, outputTokens: 33000, totalTokens: 68000 },
      { date: '2026-02-07', inputTokens: 38000, outputTokens: 35000, totalTokens: 73000 },
      { date: '2026-02-08', inputTokens: 40000, outputTokens: 37000, totalTokens: 77000 },
    ],
    latencyOverTime: [
      { date: '2026-02-04', averageLatencyMs: 1300, executionCount: 180 },
      { date: '2026-02-05', averageLatencyMs: 1280, executionCount: 195 },
      { date: '2026-02-06', averageLatencyMs: 1250, executionCount: 210 },
      { date: '2026-02-07', averageLatencyMs: 1220, executionCount: 225 },
      { date: '2026-02-08', averageLatencyMs: 1200, executionCount: 240 },
    ],
  },
  {
    agentName: 'explain-agent',
    displayName: 'Explain Agent',
    description: 'Explain complex game rules and concepts',
    modelProvider: 'OpenRouter',
    modelName: 'deepseek/deepseek-chat',
    isActive: true,
    executionCount: 876,
    totalTokens: 321456,
    inputTokens: 165432,
    outputTokens: 156024,
    averageLatencyMs: 1450,
    successRate: 0.97,
    lastExecutedAt: new Date('2026-02-07T23:45:00Z'),
    tokensOverTime: [
      { date: '2026-02-04', inputTokens: 25000, outputTokens: 23000, totalTokens: 48000 },
      { date: '2026-02-05', inputTokens: 27000, outputTokens: 25000, totalTokens: 52000 },
      { date: '2026-02-06', inputTokens: 29000, outputTokens: 27000, totalTokens: 56000 },
      { date: '2026-02-07', inputTokens: 31000, outputTokens: 29000, totalTokens: 60000 },
      { date: '2026-02-08', inputTokens: 33000, outputTokens: 31000, totalTokens: 64000 },
    ],
    latencyOverTime: [
      { date: '2026-02-04', averageLatencyMs: 1500, executionCount: 125 },
      { date: '2026-02-05', averageLatencyMs: 1480, executionCount: 140 },
      { date: '2026-02-06', averageLatencyMs: 1460, executionCount: 155 },
      { date: '2026-02-07', averageLatencyMs: 1440, executionCount: 170 },
      { date: '2026-02-08', averageLatencyMs: 1420, executionCount: 185 },
    ],
  },
  {
    agentName: 'strategy-agent',
    displayName: 'Strategy Agent',
    description: 'Provide strategic gameplay advice',
    modelProvider: 'OpenRouter',
    modelName: 'deepseek/deepseek-chat',
    isActive: true,
    executionCount: 543,
    totalTokens: 198765,
    inputTokens: 98765,
    outputTokens: 100000,
    averageLatencyMs: 1600,
    successRate: 0.96,
    lastExecutedAt: new Date('2026-02-07T22:15:00Z'),
    tokensOverTime: [
      { date: '2026-02-04', inputTokens: 15000, outputTokens: 15500, totalTokens: 30500 },
      { date: '2026-02-05', inputTokens: 17000, outputTokens: 17500, totalTokens: 34500 },
      { date: '2026-02-06', inputTokens: 19000, outputTokens: 19500, totalTokens: 38500 },
      { date: '2026-02-07', inputTokens: 21000, outputTokens: 21500, totalTokens: 42500 },
      { date: '2026-02-08', inputTokens: 23000, outputTokens: 23500, totalTokens: 46500 },
    ],
    latencyOverTime: [
      { date: '2026-02-04', averageLatencyMs: 1650, executionCount: 75 },
      { date: '2026-02-05', averageLatencyMs: 1630, executionCount: 85 },
      { date: '2026-02-06', averageLatencyMs: 1610, executionCount: 95 },
      { date: '2026-02-07', averageLatencyMs: 1590, executionCount: 105 },
      { date: '2026-02-08', averageLatencyMs: 1570, executionCount: 115 },
    ],
  },
];

export default function AgentCatalogPage() {
  const totalExecutions = mockAgents.reduce((sum, a) => sum + a.executionCount, 0);
  const totalTokens = mockAgents.reduce((sum, a) => sum + a.totalTokens, 0);
  const avgLatency = mockAgents.reduce((sum, a) => sum + a.averageLatencyMs, 0) / mockAgents.length;
  const avgSuccessRate = mockAgents.reduce((sum, a) => sum + a.successRate, 0) / mockAgents.length;

  return (
    <div className="container mx-auto space-y-6 p-6">
      <div>
        <h1 className="text-3xl font-bold">Agent Catalog</h1>
        <p className="text-muted-foreground">Usage statistics and performance metrics for all AI agents</p>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Agents</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{mockAgents.length}</div>
            <p className="text-xs text-muted-foreground">{mockAgents.filter((a) => a.isActive).length} active</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Executions</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalExecutions.toLocaleString()}</div>
            <p className="text-xs text-muted-foreground">Last 30 days</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Tokens</CardTitle>
            <Zap className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{(totalTokens / 1000000).toFixed(2)}M</div>
            <p className="text-xs text-muted-foreground">Combined usage</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Success Rate</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{(avgSuccessRate * 100).toFixed(1)}%</div>
            <p className="text-xs text-muted-foreground">{avgLatency.toFixed(0)}ms avg latency</p>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6">
        {mockAgents.map((agent) => (
          <Card key={agent.agentName}>
            <CardHeader>
              <div className="flex items-start justify-between">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2">
                    {agent.displayName}
                    {agent.isActive && <Badge variant="default">Active</Badge>}
                  </CardTitle>
                  <CardDescription>{agent.description}</CardDescription>
                  <div className="flex gap-2 pt-2 text-xs text-muted-foreground">
                    <span>{agent.modelProvider} - {agent.modelName}</span>
                  </div>
                </div>
                <div className="text-right text-sm">
                  <div className="font-semibold">{agent.executionCount.toLocaleString()}</div>
                  <div className="text-muted-foreground">executions</div>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid grid-cols-4 gap-4">
                <div>
                  <div className="text-xs text-muted-foreground">Total Tokens</div>
                  <div className="text-lg font-semibold">{(agent.totalTokens / 1000).toFixed(0)}K</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Avg Latency</div>
                  <div className="text-lg font-semibold">{agent.averageLatencyMs}ms</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Success Rate</div>
                  <div className="text-lg font-semibold">{(agent.successRate * 100).toFixed(1)}%</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Last Used</div>
                  <div className="text-lg font-semibold">{agent.lastExecutedAt?.toLocaleDateString()}</div>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <h4 className="text-sm font-medium">Token Usage (Last 5 Days)</h4>
                  <ResponsiveContainer width="100%" height={200}>
                    <LineChart data={agent.tokensOverTime}>
                      <XAxis dataKey="date" stroke="#888888" fontSize={12} />
                      <YAxis stroke="#888888" fontSize={12} />
                      <Tooltip />
                      <Line type="monotone" dataKey="inputTokens" stroke="#8b5cf6" strokeWidth={2} dot={false} name="Input" />
                      <Line type="monotone" dataKey="outputTokens" stroke="#3b82f6" strokeWidth={2} dot={false} name="Output" />
                    </LineChart>
                  </ResponsiveContainer>
                </div>

                <div className="space-y-2">
                  <h4 className="text-sm font-medium">Latency Trend (Last 5 Days)</h4>
                  <ResponsiveContainer width="100%" height={200}>
                    <LineChart data={agent.latencyOverTime}>
                      <XAxis dataKey="date" stroke="#888888" fontSize={12} />
                      <YAxis stroke="#888888" fontSize={12} />
                      <Tooltip />
                      <Line type="monotone" dataKey="averageLatencyMs" stroke="#10b981" strokeWidth={2} dot={false} name="Avg Latency" />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
