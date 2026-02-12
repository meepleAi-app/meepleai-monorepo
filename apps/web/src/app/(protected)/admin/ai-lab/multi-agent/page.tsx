/**
 * ISSUE-3778: Unified Multi-Agent Dashboard
 * Monitoring and management dashboard for Tutor, Arbitro, Decisore agents
 */

'use client';

import { BarChart, Bar, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, CartesianGrid } from 'recharts';
import { Activity, DollarSign, Zap, CheckCircle, AlertCircle, XCircle } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import { AgentSelectorBadge } from '@/components/agent/AgentSelectorBadge';
import { mockMultiAgentDashboardData, type AgentStats } from '@/lib/schemas/multi-agent-dashboard-schema';

export default function MultiAgentDashboardPage() {
  const data = mockMultiAgentDashboardData;

  return (
    <div className="container max-w-7xl py-8">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">Multi-Agent Dashboard</h1>
        <p className="text-muted-foreground mt-2">
          Monitor and manage Tutor, Arbitro, and Decisore agents
        </p>
      </div>

      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-3 mb-8">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Queries</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{data.totalQueries.toLocaleString()}</div>
            <p className="text-xs text-muted-foreground">All agents combined</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Cost</CardTitle>
            <DollarSign className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">${data.totalCost.toFixed(2)}</div>
            <p className="text-xs text-muted-foreground">Last 7 days</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Response Time</CardTitle>
            <Zap className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{data.avgResponseTime}ms</div>
            <p className="text-xs text-muted-foreground">Across all agents</p>
          </CardContent>
        </Card>
      </div>

      {/* Agent Cards */}
      <div className="grid gap-6 md:grid-cols-3 mb-8">
        {data.agents.map((agent) => (
          <AgentCard key={agent.agentType} agent={agent} />
        ))}
      </div>

      {/* Cost Trend Chart */}
      <Card className="mb-8">
        <CardHeader>
          <CardTitle>Cost Trend (Last 7 Days)</CardTitle>
          <CardDescription>Daily cost breakdown by agent</CardDescription>
        </CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={data.costTrend}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
              <XAxis dataKey="date" className="text-xs" />
              <YAxis className="text-xs" />
              <Tooltip
                contentStyle={{ backgroundColor: 'hsl(var(--card))', border: '1px solid hsl(var(--border))' }}
              />
              <Legend />
              <Bar dataKey="tutor" fill="#3b82f6" name="Tutor" />
              <Bar dataKey="arbitro" fill="#eab308" name="Arbitro" />
              <Bar dataKey="decisore" fill="#a855f7" name="Decisore" />
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </div>
  );
}

// ========== Sub-Components ==========

function AgentCard({ agent }: { agent: AgentStats }) {
  const agentName = agent.agentType.charAt(0).toUpperCase() + agent.agentType.slice(1);

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg">{agentName}</CardTitle>
          <AgentSelectorBadge currentAgent={agent.agentType} />
        </div>
        <CardDescription>{getAgentDescription(agent.agentType)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Status */}
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Status</span>
          <StatusBadge status={agent.status} />
        </div>

        {/* Queries */}
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Queries</span>
          <span className="text-sm font-medium">{agent.totalQueries.toLocaleString()}</span>
        </div>

        {/* Success Rate */}
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Success Rate</span>
          <span className="text-sm font-medium">{agent.successRate.toFixed(1)}%</span>
        </div>

        {/* Response Time */}
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Avg Response</span>
          <span className="text-sm font-medium">{agent.avgResponseTime}ms</span>
        </div>

        {/* P95 */}
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">P95</span>
          <span className="text-sm font-medium">{agent.p95ResponseTime}ms</span>
        </div>

        {/* Cost */}
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Cost (7d)</span>
          <span className="text-sm font-medium">${agent.totalCost.toFixed(2)}</span>
        </div>
      </CardContent>
    </Card>
  );
}

function StatusBadge({ status }: { status: 'healthy' | 'degraded' | 'offline' }) {
  const config = {
    healthy: { icon: CheckCircle, label: 'Healthy', variant: 'default' as const, color: 'text-green-600' },
    degraded: { icon: AlertCircle, label: 'Degraded', variant: 'secondary' as const, color: 'text-yellow-600' },
    offline: { icon: XCircle, label: 'Offline', variant: 'destructive' as const, color: 'text-red-600' },
  };

  const { icon: Icon, label, variant, color } = config[status];

  return (
    <Badge variant={variant} className="flex items-center gap-1">
      <Icon className={`h-3 w-3 ${color}`} />
      {label}
    </Badge>
  );
}

function getAgentDescription(agentType: string): string {
  const descriptions = {
    tutor: 'Rules, setup, and tutorial questions',
    arbitro: 'Move validation and rules arbitration',
    decisore: 'Strategic analysis and move suggestions',
  };
  return descriptions[agentType as keyof typeof descriptions] || '';
}
