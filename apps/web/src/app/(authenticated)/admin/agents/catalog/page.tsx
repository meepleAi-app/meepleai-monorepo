'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  Activity,
  CheckCircle,
  Clock,
  Copy,
  DollarSign,
  Download,
  Grid3X3,
  List,
  Pencil,
  Trash2,
  Users,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import {
  Badge,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import type {
  AgentCatalogAgentStats,
  AgentCatalogStatsResult,
} from '@/lib/api/agent-definitions.api';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

type DateRange = '7d' | '14d' | '30d' | '90d';
type ViewMode = 'grid' | 'list';

// ============================================================================
// Formatters
// ============================================================================

function formatNumber(num: number): string {
  if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M`;
  if (num >= 1_000) return `${(num / 1_000).toFixed(1)}K`;
  return num.toLocaleString();
}

function formatCost(cost: number): string {
  if (cost >= 1000) return `€${(cost / 1000).toFixed(2)}K`;
  if (cost >= 1) return `€${cost.toFixed(2)}`;
  return `€${cost.toFixed(4)}`;
}

function formatLatency(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
  return `${Math.round(ms)}ms`;
}

function formatPercent(rate: number): string {
  return `${(rate * 100).toFixed(1)}%`;
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  return new Date(dateStr).toLocaleDateString();
}

// ============================================================================
// KPI Cards
// ============================================================================

function KpiCard({
  icon: Icon,
  iconColor,
  label,
  value,
  subValue,
}: {
  icon: React.ElementType;
  iconColor: string;
  label: string;
  value: string;
  subValue?: string;
}) {
  return (
    <Card className="relative overflow-hidden">
      <CardContent className="p-4">
        <div className="flex items-start gap-3">
          <div className={cn('flex h-10 w-10 shrink-0 items-center justify-center rounded-xl', iconColor)}>
            <Icon className="h-5 w-5 text-white" />
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-2xl font-bold tracking-tight">{value}</p>
            <p className="text-sm text-muted-foreground">{label}</p>
            {subValue && <p className="mt-1 text-xs text-muted-foreground">{subValue}</p>}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================================================
// Agent Grid Card
// ============================================================================

function AgentGridCard({
  agent,
  onEdit,
  onClone,
  onDelete,
  onExport,
  expanded,
  onToggleExpand,
}: {
  agent: AgentCatalogAgentStats;
  onEdit: () => void;
  onClone: () => void;
  onDelete: () => void;
  onExport: () => void;
  expanded: boolean;
  onToggleExpand: () => void;
}) {
  return (
    <Card className="transition-shadow hover:shadow-md">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between">
          <div className="space-y-1">
            <CardTitle className="flex items-center gap-2 text-lg">
              <button type="button" onClick={onToggleExpand} className="hover:underline text-left">
                {agent.name}
              </button>
              <Badge variant={agent.isActive ? 'default' : 'secondary'}>
                {agent.isActive ? 'Active' : 'Inactive'}
              </Badge>
            </CardTitle>
            {agent.description && (
              <CardDescription className="line-clamp-2">{agent.description}</CardDescription>
            )}
            <div className="flex gap-2 pt-1 text-xs text-muted-foreground">
              <span>{agent.type}</span>
              {agent.model && <span>• {agent.model}</span>}
            </div>
          </div>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                <span className="sr-only">Actions</span>
                <svg width="15" height="15" viewBox="0 0 15 15" fill="currentColor">
                  <circle cx="7.5" cy="2.5" r="1.5" />
                  <circle cx="7.5" cy="7.5" r="1.5" />
                  <circle cx="7.5" cy="12.5" r="1.5" />
                </svg>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={onEdit}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </DropdownMenuItem>
              <DropdownMenuItem onClick={onClone}>
                <Copy className="mr-2 h-4 w-4" /> Clone
              </DropdownMenuItem>
              <DropdownMenuItem onClick={onExport}>
                <Download className="mr-2 h-4 w-4" /> Export Stats
              </DropdownMenuItem>
              <DropdownMenuItem onClick={onDelete} className="text-destructive">
                <Trash2 className="mr-2 h-4 w-4" /> Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div>
            <div className="text-xs text-muted-foreground">Executions</div>
            <div className="text-lg font-semibold">{formatNumber(agent.executionCount)}</div>
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Avg Tokens</div>
            <div className="text-lg font-semibold">{formatNumber(agent.avgTokens)}</div>
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Success Rate</div>
            <div className="text-lg font-semibold">{formatPercent(agent.successRate)}</div>
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Avg Latency</div>
            <div className="text-lg font-semibold">{formatLatency(agent.avgLatencyMs)}</div>
          </div>
        </div>

        {/* Sparkline */}
        {agent.timeSeries.length > 1 && (
          <div className="h-16">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={agent.timeSeries}>
                <defs>
                  <linearGradient id={`spark-${agent.agentDefinitionId}`} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#8b5cf6" stopOpacity={0.3} />
                    <stop offset="100%" stopColor="#8b5cf6" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <Area
                  type="monotone"
                  dataKey="executions"
                  stroke="#8b5cf6"
                  fill={`url(#spark-${agent.agentDefinitionId})`}
                  strokeWidth={1.5}
                  dot={false}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        )}

        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span>Cost: {formatCost(agent.totalCost)}</span>
          <span>Last used: {formatDate(agent.lastExecutedAt)}</span>
        </div>

        {/* Expanded detail charts */}
        {expanded && agent.timeSeries.length > 1 && (
          <div className="grid gap-4 border-t pt-4 md:grid-cols-2">
            <div className="space-y-2">
              <h4 className="text-sm font-medium">Executions Over Time</h4>
              <ResponsiveContainer width="100%" height={180}>
                <BarChart data={agent.timeSeries}>
                  <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                  <XAxis dataKey="date" stroke="#888888" fontSize={10} tickFormatter={(v: string) => v.slice(5)} />
                  <YAxis stroke="#888888" fontSize={10} />
                  <Tooltip />
                  <Bar dataKey="executions" fill="#8b5cf6" radius={[2, 2, 0, 0]} name="Executions" />
                </BarChart>
              </ResponsiveContainer>
            </div>
            <div className="space-y-2">
              <h4 className="text-sm font-medium">Latency Trend</h4>
              <ResponsiveContainer width="100%" height={180}>
                <AreaChart data={agent.timeSeries}>
                  <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                  <XAxis dataKey="date" stroke="#888888" fontSize={10} tickFormatter={(v: string) => v.slice(5)} />
                  <YAxis stroke="#888888" fontSize={10} />
                  <Tooltip />
                  <Area type="monotone" dataKey="avgLatencyMs" stroke="#10b981" fill="#10b98120" strokeWidth={2} dot={false} name="Avg Latency (ms)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ============================================================================
// Agent List Table
// ============================================================================

function AgentListTable({
  agents,
  onEdit,
  onClone,
  onDelete,
  onExport,
}: {
  agents: AgentCatalogAgentStats[];
  onEdit: (id: string) => void;
  onClone: (id: string) => void;
  onDelete: (id: string) => void;
  onExport: (agent: AgentCatalogAgentStats) => void;
}) {
  const [sortKey, setSortKey] = useState<string>('executionCount');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

  const sorted = [...agents].sort((a, b) => {
    const aVal = a[sortKey as keyof AgentCatalogAgentStats] ?? 0;
    const bVal = b[sortKey as keyof AgentCatalogAgentStats] ?? 0;
    if (typeof aVal === 'number' && typeof bVal === 'number') {
      return sortDir === 'desc' ? bVal - aVal : aVal - bVal;
    }
    return 0;
  });

  const toggleSort = (key: string) => {
    if (sortKey === key) {
      setSortDir((d) => (d === 'desc' ? 'asc' : 'desc'));
    } else {
      setSortKey(key);
      setSortDir('desc');
    }
  };

  const SortHeader = ({ label, field }: { label: string; field: string }) => (
    <TableHead className="cursor-pointer select-none" onClick={() => toggleSort(field)}>
      {label} {sortKey === field && (sortDir === 'desc' ? '↓' : '↑')}
    </TableHead>
  );

  return (
    <Card>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Agent</TableHead>
            <SortHeader label="Executions" field="executionCount" />
            <SortHeader label="Avg Tokens" field="avgTokens" />
            <SortHeader label="Cost" field="totalCost" />
            <SortHeader label="Success" field="successRate" />
            <SortHeader label="Latency" field="avgLatencyMs" />
            <TableHead>Last Used</TableHead>
            <TableHead className="w-[60px]" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {sorted.map((agent) => (
            <TableRow key={agent.agentDefinitionId}>
              <TableCell>
                <div className="flex items-center gap-2">
                  <div>
                    <div className="font-medium">{agent.name}</div>
                    <div className="text-xs text-muted-foreground">{agent.type} {agent.model && `• ${agent.model}`}</div>
                  </div>
                  <Badge variant={agent.isActive ? 'default' : 'secondary'} className="ml-1">
                    {agent.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </div>
              </TableCell>
              <TableCell className="font-mono">{formatNumber(agent.executionCount)}</TableCell>
              <TableCell className="font-mono">{formatNumber(agent.avgTokens)}</TableCell>
              <TableCell className="font-mono">{formatCost(agent.totalCost)}</TableCell>
              <TableCell>
                <span className={cn(
                  'font-mono',
                  agent.successRate >= 0.95 ? 'text-emerald-600' :
                  agent.successRate >= 0.8 ? 'text-amber-600' : 'text-destructive'
                )}>
                  {formatPercent(agent.successRate)}
                </span>
              </TableCell>
              <TableCell className="font-mono">{formatLatency(agent.avgLatencyMs)}</TableCell>
              <TableCell className="text-muted-foreground">{formatDate(agent.lastExecutedAt)}</TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                      <svg width="15" height="15" viewBox="0 0 15 15" fill="currentColor">
                        <circle cx="7.5" cy="2.5" r="1.5" />
                        <circle cx="7.5" cy="7.5" r="1.5" />
                        <circle cx="7.5" cy="12.5" r="1.5" />
                      </svg>
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => onEdit(agent.agentDefinitionId)}>
                      <Pencil className="mr-2 h-4 w-4" /> Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => onClone(agent.agentDefinitionId)}>
                      <Copy className="mr-2 h-4 w-4" /> Clone
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => onExport(agent)}>
                      <Download className="mr-2 h-4 w-4" /> Export Stats
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => onDelete(agent.agentDefinitionId)} className="text-destructive">
                      <Trash2 className="mr-2 h-4 w-4" /> Delete
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          ))}
          {sorted.length === 0 && (
            <TableRow>
              <TableCell colSpan={8} className="h-24 text-center text-muted-foreground">
                No agents found.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </Card>
  );
}

// ============================================================================
// Main Page
// ============================================================================

const DATE_RANGES: { label: string; value: DateRange }[] = [
  { label: '7 days', value: '7d' },
  { label: '14 days', value: '14d' },
  { label: '30 days', value: '30d' },
  { label: '90 days', value: '90d' },
];

export default function AgentCatalogPage() {
  const router = useRouter();
  const [data, setData] = useState<AgentCatalogStatsResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [range, setRange] = useState<DateRange>('30d');
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [expandedAgent, setExpandedAgent] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await agentDefinitionsApi.getCatalogStats({ range });
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch catalog stats');
    } finally {
      setIsLoading(false);
    }
  }, [range]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleEdit = (id: string) => {
    router.push(`/admin/agent-definitions/${id}/edit`);
  };

  const handleClone = async (id: string) => {
    try {
      await agentDefinitionsApi.clone(id);
      fetchData();
    } catch {
      setError('Failed to clone agent');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this agent? This action cannot be undone.')) {
      return;
    }
    try {
      await agentDefinitionsApi.delete(id);
      fetchData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete agent');
    }
  };

  const handleExport = (agent: AgentCatalogAgentStats) => {
    const csvRows = [
      ['Date', 'Executions', 'Total Tokens', 'Cost', 'Avg Latency (ms)', 'Success Rate'],
      ...agent.timeSeries.map((ts) => [
        ts.date,
        ts.executions.toString(),
        ts.totalTokens.toString(),
        ts.cost.toFixed(4),
        ts.avgLatencyMs.toFixed(1),
        (ts.successRate * 100).toFixed(1) + '%',
      ]),
    ];
    const csv = csvRows.map((row) => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${agent.name.replace(/\s+/g, '_')}_stats_${range}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const global = data?.global;
  const agents = data?.agents ?? [];

  return (
    <div className="container mx-auto space-y-6 p-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="font-quicksand text-3xl font-bold">Agent Catalog</h1>
          <p className="text-muted-foreground">Usage statistics and performance metrics for all AI agents</p>
        </div>
        <div className="flex items-center gap-2">
          {/* Date Range Selector */}
          <div className="flex rounded-lg border bg-muted/50 p-0.5">
            {DATE_RANGES.map((r) => (
              <button
                key={r.value}
                type="button"
                onClick={() => setRange(r.value)}
                className={cn(
                  'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                  range === r.value
                    ? 'bg-background shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
              >
                {r.label}
              </button>
            ))}
          </div>
          {/* View Toggle */}
          <div className="flex rounded-lg border bg-muted/50 p-0.5">
            <button
              type="button"
              onClick={() => setViewMode('grid')}
              className={cn(
                'rounded-md p-1.5 transition-colors',
                viewMode === 'grid' ? 'bg-background shadow-sm' : 'text-muted-foreground hover:text-foreground'
              )}
              aria-label="Grid view"
            >
              <Grid3X3 className="h-4 w-4" />
            </button>
            <button
              type="button"
              onClick={() => setViewMode('list')}
              className={cn(
                'rounded-md p-1.5 transition-colors',
                viewMode === 'list' ? 'bg-background shadow-sm' : 'text-muted-foreground hover:text-foreground'
              )}
              aria-label="List view"
            >
              <List className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <Card className="border-destructive">
          <CardContent className="p-4 text-destructive">{error}</CardContent>
        </Card>
      )}

      {/* KPI Cards */}
      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {[...Array(4)].map((_, i) => (
            <Card key={i}>
              <CardContent className="p-4">
                <div className="flex items-start gap-3">
                  <Skeleton className="h-10 w-10 rounded-xl" />
                  <div className="flex-1 space-y-2">
                    <Skeleton className="h-7 w-20" />
                    <Skeleton className="h-4 w-24" />
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : global ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <KpiCard
            icon={Activity}
            iconColor="bg-blue-500"
            label="Total Executions"
            value={formatNumber(global.totalExecutions)}
            subValue={`${global.totalAgents} agents (${global.activeAgents} active)`}
          />
          <KpiCard
            icon={DollarSign}
            iconColor="bg-emerald-500"
            label="Total Cost"
            value={formatCost(global.totalCost)}
            subValue={`Last ${range}`}
          />
          <KpiCard
            icon={CheckCircle}
            iconColor="bg-purple-500"
            label="Avg Success Rate"
            value={formatPercent(global.avgSuccessRate)}
            subValue={`${formatPercent(global.avgConfidence)} confidence`}
          />
          <KpiCard
            icon={Clock}
            iconColor="bg-amber-500"
            label="Avg Latency"
            value={formatLatency(global.avgLatencyMs)}
            subValue="Response time"
          />
        </div>
      ) : null}

      {/* Agent Grid/List */}
      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {[...Array(3)].map((_, i) => (
            <Card key={i}>
              <CardContent className="space-y-3 p-6">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-60" />
                <div className="grid grid-cols-4 gap-2">
                  {[...Array(4)].map((_, j) => (
                    <Skeleton key={j} className="h-12" />
                  ))}
                </div>
                <Skeleton className="h-16 w-full" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : viewMode === 'grid' ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {agents.map((agent) => (
            <AgentGridCard
              key={agent.agentDefinitionId}
              agent={agent}
              onEdit={() => handleEdit(agent.agentDefinitionId)}
              onClone={() => handleClone(agent.agentDefinitionId)}
              onDelete={() => handleDelete(agent.agentDefinitionId)}
              onExport={() => handleExport(agent)}
              expanded={expandedAgent === agent.agentDefinitionId}
              onToggleExpand={() =>
                setExpandedAgent(
                  expandedAgent === agent.agentDefinitionId ? null : agent.agentDefinitionId
                )
              }
            />
          ))}
          {agents.length === 0 && (
            <Card className="col-span-full">
              <CardContent className="flex flex-col items-center gap-3 p-12">
                <Users className="h-12 w-12 text-muted-foreground/50" />
                <p className="text-lg font-medium text-muted-foreground">No agents defined yet</p>
                <Button onClick={() => router.push('/admin/agent-definitions/create')}>
                  Create Agent
                </Button>
              </CardContent>
            </Card>
          )}
        </div>
      ) : (
        <AgentListTable
          agents={agents}
          onEdit={handleEdit}
          onClone={handleClone}
          onDelete={handleDelete}
          onExport={handleExport}
        />
      )}
    </div>
  );
}
