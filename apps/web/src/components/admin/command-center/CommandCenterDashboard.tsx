/**
 * Command Center Dashboard - Admin Mission Control
 *
 * A distinctive "Mission Control" aesthetic for admin monitoring:
 * - Dark theme with high-contrast data visualization
 * - Real-time status indicators with glowing effects
 * - Hexagonal grid layout for service monitoring
 * - Terminal-inspired typography (JetBrains Mono)
 * - Animated pulse indicators for live data
 *
 * Issue #3286 - Admin Dashboard Redesign
 */

'use client';

import { useState, useEffect, useMemo } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Clock,
  Cpu,
  Database,
  HardDrive,
  Network,
  Server,
  Shield,
  Zap,
  Users,
  FileText,
  MessageSquare,
  Bot,
  RefreshCw,
  ChevronRight,
  Terminal,
  Gauge,
} from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

// =============================================================================
// Types
// =============================================================================

export type ServiceStatus = 'online' | 'degraded' | 'offline' | 'maintenance';

export interface ServiceData {
  id: string;
  name: string;
  status: ServiceStatus;
  latency?: number;
  uptime?: number;
  lastCheck?: Date;
  icon: React.ElementType;
}

export interface SystemMetric {
  id: string;
  label: string;
  value: string | number;
  unit?: string;
  trend?: number;
  status?: 'good' | 'warning' | 'critical';
}

export interface AlertData {
  id: string;
  severity: 'critical' | 'warning' | 'info';
  message: string;
  timestamp: Date;
  source: string;
}

export interface PendingAction {
  id: string;
  type: 'approval' | 'moderation' | 'review';
  title: string;
  count: number;
  href: string;
}

// =============================================================================
// Mock Data
// =============================================================================

const mockServices: ServiceData[] = [
  { id: 'api', name: 'API Gateway', status: 'online', latency: 42, uptime: 99.97, icon: Server },
  { id: 'postgres', name: 'PostgreSQL', status: 'online', latency: 8, uptime: 99.99, icon: Database },
  { id: 'redis', name: 'Redis Cache', status: 'online', latency: 2, uptime: 100, icon: Zap },
  { id: 'qdrant', name: 'Qdrant Vector', status: 'online', latency: 15, uptime: 99.95, icon: HardDrive },
  { id: 'embedding', name: 'Embedding Service', status: 'degraded', latency: 180, uptime: 98.5, icon: Bot },
  { id: 'reranker', name: 'Reranker Service', status: 'online', latency: 95, uptime: 99.8, icon: Cpu },
  { id: 'smoldocling', name: 'SmolDocling', status: 'maintenance', latency: 0, uptime: 95.2, icon: FileText },
  { id: 'unstructured', name: 'Unstructured', status: 'online', latency: 250, uptime: 99.1, icon: FileText },
];

const mockMetrics: SystemMetric[] = [
  { id: 'users', label: 'Total Users', value: 2847, trend: 12.5, status: 'good' },
  { id: 'sessions', label: 'Active Sessions', value: 342, trend: 8.2, status: 'good' },
  { id: 'games', label: 'Games in Catalog', value: 15623, trend: 2.1, status: 'good' },
  { id: 'api-24h', label: 'API Calls (24h)', value: '1.2M', trend: -3.4, status: 'warning' },
  { id: 'latency', label: 'Avg Latency', value: 127, unit: 'ms', trend: -15.2, status: 'good' },
  { id: 'error-rate', label: 'Error Rate', value: 0.12, unit: '%', trend: 5.1, status: 'warning' },
  { id: 'tokens', label: 'Tokens Used', value: '45.2M', trend: 18.7, status: 'good' },
  { id: 'rag-requests', label: 'RAG Requests', value: 8934, trend: 22.3, status: 'good' },
];

const mockAlerts: AlertData[] = [
  { id: '1', severity: 'warning', message: 'Embedding service latency above threshold (180ms)', timestamp: new Date(Date.now() - 300000), source: 'embedding-service' },
  { id: '2', severity: 'info', message: 'SmolDocling maintenance window in progress', timestamp: new Date(Date.now() - 600000), source: 'smoldocling' },
  { id: '3', severity: 'critical', message: 'High memory usage on reranker-service (92%)', timestamp: new Date(Date.now() - 900000), source: 'reranker-service' },
];

const mockPendingActions: PendingAction[] = [
  { id: '1', type: 'approval', title: 'Game Approvals', count: 12, href: '/admin/shared-games/pending-approvals' },
  { id: '2', type: 'moderation', title: 'Content Review', count: 5, href: '/admin/games' },
  { id: '3', type: 'review', title: 'Share Requests', count: 8, href: '/admin/share-requests' },
];

// =============================================================================
// Utility Functions
// =============================================================================

const getStatusColor = (status: ServiceStatus) => {
  switch (status) {
    case 'online': return { bg: 'bg-emerald-500', glow: 'shadow-emerald-500/50', text: 'text-emerald-400' };
    case 'degraded': return { bg: 'bg-amber-500', glow: 'shadow-amber-500/50', text: 'text-amber-400' };
    case 'offline': return { bg: 'bg-red-500', glow: 'shadow-red-500/50', text: 'text-red-400' };
    case 'maintenance': return { bg: 'bg-blue-500', glow: 'shadow-blue-500/50', text: 'text-blue-400' };
  }
};

const getSeverityStyles = (severity: 'critical' | 'warning' | 'info') => {
  switch (severity) {
    case 'critical': return { bg: 'bg-red-500/10', border: 'border-red-500/30', text: 'text-red-400', icon: XCircle };
    case 'warning': return { bg: 'bg-amber-500/10', border: 'border-amber-500/30', text: 'text-amber-400', icon: AlertTriangle };
    case 'info': return { bg: 'bg-blue-500/10', border: 'border-blue-500/30', text: 'text-blue-400', icon: Activity };
  }
};

const formatTimeAgo = (date: Date) => {
  const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
  return `${Math.floor(seconds / 86400)}d ago`;
};

// =============================================================================
// Sub-Components
// =============================================================================

/** Animated status indicator with glow effect */
function StatusIndicator({ status }: { status: ServiceStatus }) {
  const colors = getStatusColor(status);

  return (
    <div className="relative">
      <div className={cn(
        'w-3 h-3 rounded-full',
        colors.bg,
        status === 'online' && 'animate-pulse'
      )} />
      <div className={cn(
        'absolute inset-0 w-3 h-3 rounded-full blur-sm',
        colors.bg,
        'opacity-60'
      )} />
    </div>
  );
}

/** Service card with hexagonal-inspired design */
function ServiceCard({ service }: { service: ServiceData }) {
  const colors = getStatusColor(service.status);
  const Icon = service.icon;

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      className={cn(
        'relative group',
        'bg-slate-900/80 backdrop-blur-xl',
        'border border-slate-700/50',
        'rounded-lg overflow-hidden',
        'hover:border-slate-600/80 transition-all duration-300',
        'hover:shadow-lg',
        colors.glow.replace('shadow-', 'hover:shadow-')
      )}
    >
      {/* Status bar at top */}
      <div className={cn('h-1 w-full', colors.bg)} />

      <div className="p-4">
        <div className="flex items-start justify-between mb-3">
          <div className={cn(
            'w-10 h-10 rounded-lg flex items-center justify-center',
            'bg-slate-800/80 border border-slate-700/50',
            colors.text
          )}>
            <Icon className="w-5 h-5" />
          </div>
          <StatusIndicator status={service.status} />
        </div>

        <h3 className="font-mono text-sm font-medium text-slate-200 mb-1">
          {service.name}
        </h3>

        <div className="flex items-center justify-between text-xs">
          <span className={cn('font-mono uppercase tracking-wider', colors.text)}>
            {service.status}
          </span>
          {service.latency !== undefined && service.latency > 0 && (
            <span className="font-mono text-slate-500">
              {service.latency}ms
            </span>
          )}
        </div>

        {service.uptime !== undefined && (
          <div className="mt-3 pt-3 border-t border-slate-800">
            <div className="flex items-center justify-between text-xs">
              <span className="text-slate-500">Uptime</span>
              <span className={cn(
                'font-mono',
                service.uptime >= 99.9 ? 'text-emerald-400' :
                service.uptime >= 99 ? 'text-amber-400' : 'text-red-400'
              )}>
                {service.uptime}%
              </span>
            </div>
            <div className="mt-1 h-1 bg-slate-800 rounded-full overflow-hidden">
              <div
                className={cn(
                  'h-full rounded-full transition-all duration-500',
                  service.uptime >= 99.9 ? 'bg-emerald-500' :
                  service.uptime >= 99 ? 'bg-amber-500' : 'bg-red-500'
                )}
                style={{ width: `${service.uptime}%` }}
              />
            </div>
          </div>
        )}
      </div>
    </motion.div>
  );
}

/** KPI metric card with trend indicator */
function MetricCard({ metric, index }: { metric: SystemMetric; index: number }) {
  const isPositiveTrend = metric.trend && metric.trend > 0;
  const trendColor = metric.status === 'critical' ? 'text-red-400' :
                     metric.status === 'warning' ? 'text-amber-400' : 'text-emerald-400';

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
      className={cn(
        'relative overflow-hidden',
        'bg-slate-900/60 backdrop-blur-xl',
        'border border-slate-700/50 rounded-lg',
        'p-4 group hover:border-slate-600/80 transition-all'
      )}
    >
      {/* Decorative corner gradient */}
      <div className="absolute -top-10 -right-10 w-20 h-20 bg-gradient-to-br from-cyan-500/10 to-transparent rounded-full blur-xl opacity-0 group-hover:opacity-100 transition-opacity" />

      <p className="text-xs text-slate-500 uppercase tracking-wider font-mono mb-2">
        {metric.label}
      </p>

      <div className="flex items-baseline gap-2">
        <span className="text-2xl font-bold text-slate-100 font-mono">
          {metric.value}
        </span>
        {metric.unit && (
          <span className="text-sm text-slate-500 font-mono">{metric.unit}</span>
        )}
      </div>

      {metric.trend !== undefined && (
        <div className={cn('flex items-center gap-1 mt-2 text-xs font-mono', trendColor)}>
          <span>{isPositiveTrend ? '↑' : '↓'}</span>
          <span>{Math.abs(metric.trend)}%</span>
          <span className="text-slate-600">vs last week</span>
        </div>
      )}
    </motion.div>
  );
}

/** Alert item with severity styling */
function AlertItem({ alert, index }: { alert: AlertData; index: number }) {
  const styles = getSeverityStyles(alert.severity);
  const Icon = styles.icon;

  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.1 }}
      className={cn(
        'flex items-start gap-3 p-3 rounded-lg border',
        styles.bg,
        styles.border
      )}
    >
      <Icon className={cn('w-5 h-5 mt-0.5 flex-shrink-0', styles.text)} />
      <div className="flex-1 min-w-0">
        <p className="text-sm text-slate-300">{alert.message}</p>
        <div className="flex items-center gap-2 mt-1 text-xs text-slate-500 font-mono">
          <span>{alert.source}</span>
          <span>•</span>
          <span>{formatTimeAgo(alert.timestamp)}</span>
        </div>
      </div>
    </motion.div>
  );
}

/** Pending action card with count badge */
function PendingActionCard({ action }: { action: PendingAction }) {
  const typeStyles = {
    approval: { icon: CheckCircle2, color: 'text-emerald-400', bg: 'bg-emerald-500/10' },
    moderation: { icon: Shield, color: 'text-amber-400', bg: 'bg-amber-500/10' },
    review: { icon: MessageSquare, color: 'text-blue-400', bg: 'bg-blue-500/10' },
  };
  const style = typeStyles[action.type];
  const Icon = style.icon;

  return (
    <Link href={action.href}>
      <motion.div
        whileHover={{ scale: 1.02 }}
        className={cn(
          'flex items-center justify-between p-4 rounded-lg',
          'bg-slate-900/60 border border-slate-700/50',
          'hover:border-slate-600/80 hover:bg-slate-800/60',
          'transition-all cursor-pointer group'
        )}
      >
        <div className="flex items-center gap-3">
          <div className={cn('w-10 h-10 rounded-lg flex items-center justify-center', style.bg)}>
            <Icon className={cn('w-5 h-5', style.color)} />
          </div>
          <div>
            <p className="text-sm font-medium text-slate-200">{action.title}</p>
            <p className="text-xs text-slate-500 font-mono uppercase tracking-wider">
              {action.type}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className={cn(
            'px-2.5 py-1 rounded-full text-xs font-bold font-mono',
            action.count > 10 ? 'bg-red-500/20 text-red-400' :
            action.count > 5 ? 'bg-amber-500/20 text-amber-400' :
            'bg-emerald-500/20 text-emerald-400'
          )}>
            {action.count}
          </span>
          <ChevronRight className="w-4 h-4 text-slate-600 group-hover:text-slate-400 transition-colors" />
        </div>
      </motion.div>
    </Link>
  );
}

/** Live terminal-style log display */
function TerminalLogs() {
  const [logs, setLogs] = useState<string[]>([
    '[INFO] System health check completed',
    '[INFO] Cache warmup: 847 entries loaded',
    '[WARN] Embedding queue depth: 23 items',
    '[INFO] User session: admin@meepleai.com',
    '[INFO] API rate: 847 req/min',
  ]);

  useEffect(() => {
    const interval = setInterval(() => {
      const newLogs = [
        `[INFO] Heartbeat OK - ${new Date().toLocaleTimeString()}`,
        `[INFO] Active connections: ${Math.floor(Math.random() * 100) + 50}`,
        `[INFO] Cache hit rate: ${(Math.random() * 5 + 95).toFixed(1)}%`,
      ];
      const newLog = newLogs[Math.floor(Math.random() * newLogs.length)];
      if (newLog) {
        setLogs(prev => [...prev.slice(-4), newLog]);
      }
    }, 5000);

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="bg-slate-950 rounded-lg border border-slate-800 overflow-hidden">
      <div className="flex items-center gap-2 px-4 py-2 border-b border-slate-800 bg-slate-900/50">
        <Terminal className="w-4 h-4 text-slate-500" />
        <span className="text-xs font-mono text-slate-500 uppercase tracking-wider">System Logs</span>
        <div className="ml-auto flex items-center gap-1">
          <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
          <span className="text-xs font-mono text-slate-500">LIVE</span>
        </div>
      </div>
      <div className="p-4 font-mono text-xs space-y-1 max-h-36 overflow-y-auto">
        <AnimatePresence mode="popLayout">
          {logs.map((log, i) => (
            <motion.div
              key={`${log}-${i}`}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
              className={cn(
                log.includes('[WARN]') ? 'text-amber-400' :
                log.includes('[ERROR]') ? 'text-red-400' :
                'text-slate-400'
              )}
            >
              {log}
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export interface CommandCenterDashboardProps {
  className?: string;
}

export function CommandCenterDashboard({ className }: CommandCenterDashboardProps) {
  const [mounted, setMounted] = useState(false);
  const [currentTime, setCurrentTime] = useState<Date | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Hydration-safe mounting
  useEffect(() => {
    setMounted(true);
    setCurrentTime(new Date());
  }, []);

  // Real-time clock
  useEffect(() => {
    if (!mounted) return;
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, [mounted]);

  // Calculate overall system health
  const systemHealth = useMemo(() => {
    const online = mockServices.filter(s => s.status === 'online').length;
    const total = mockServices.length;
    const percentage = Math.round((online / total) * 100);
    return { online, total, percentage };
  }, []);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await new Promise(resolve => setTimeout(resolve, 1500));
    setIsRefreshing(false);
  };

  return (
    <div className={cn(
      'min-h-screen bg-slate-950 text-slate-100',
      'font-sans',
      className
    )}>
      {/* Subtle grid background */}
      <div
        className="fixed inset-0 opacity-[0.02] pointer-events-none"
        style={{
          backgroundImage: `
            linear-gradient(rgba(148, 163, 184, 0.1) 1px, transparent 1px),
            linear-gradient(90deg, rgba(148, 163, 184, 0.1) 1px, transparent 1px)
          `,
          backgroundSize: '40px 40px',
        }}
      />

      <div className="relative z-10 p-6 max-w-[1800px] mx-auto">
        {/* Header */}
        <header className="mb-8">
          <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
            <div>
              <div className="flex items-center gap-3 mb-2">
                <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center shadow-lg shadow-cyan-500/25">
                  <Gauge className="w-6 h-6 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl font-bold tracking-tight">Command Center</h1>
                  <p className="text-sm text-slate-500 font-mono">MeepleAI Admin Dashboard</p>
                </div>
              </div>
            </div>

            <div className="flex items-center gap-4">
              {/* System health indicator */}
              <div className="flex items-center gap-3 px-4 py-2 rounded-lg bg-slate-900/60 border border-slate-700/50">
                <div className={cn(
                  'w-3 h-3 rounded-full',
                  systemHealth.percentage >= 90 ? 'bg-emerald-500' :
                  systemHealth.percentage >= 70 ? 'bg-amber-500' : 'bg-red-500',
                  'animate-pulse'
                )} />
                <div className="text-sm">
                  <span className="font-mono font-bold">{systemHealth.percentage}%</span>
                  <span className="text-slate-500 ml-2">System Health</span>
                </div>
              </div>

              {/* Time display */}
              {mounted && currentTime && (
                <div className="hidden md:flex items-center gap-2 px-4 py-2 rounded-lg bg-slate-900/60 border border-slate-700/50">
                  <Clock className="w-4 h-4 text-slate-500" />
                  <span className="font-mono text-sm text-slate-300">
                    {currentTime.toLocaleDateString('it-IT')}
                  </span>
                  <span className="font-mono text-sm text-cyan-400 font-bold">
                    {currentTime.toLocaleTimeString('it-IT')}
                  </span>
                </div>
              )}

              {/* Refresh button */}
              <button
                onClick={() => void handleRefresh()}
                disabled={isRefreshing}
                className={cn(
                  'p-2 rounded-lg',
                  'bg-slate-900/60 border border-slate-700/50',
                  'hover:border-slate-600/80 hover:bg-slate-800/60',
                  'transition-all disabled:opacity-50'
                )}
              >
                <RefreshCw className={cn(
                  'w-5 h-5 text-slate-400',
                  isRefreshing && 'animate-spin'
                )} />
              </button>
            </div>
          </div>
        </header>

        {/* Services Grid */}
        <section className="mb-8">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold flex items-center gap-2">
              <Network className="w-5 h-5 text-cyan-400" />
              Infrastructure Status
            </h2>
            <Link
              href="/admin/infrastructure"
              className="text-sm text-slate-500 hover:text-slate-300 transition-colors flex items-center gap-1"
            >
              View Details <ChevronRight className="w-4 h-4" />
            </Link>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-3">
            {mockServices.map((service, i) => (
              <motion.div
                key={service.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.05 }}
              >
                <ServiceCard service={service} />
              </motion.div>
            ))}
          </div>
        </section>

        {/* Metrics Grid */}
        <section className="mb-8">
          <h2 className="text-lg font-semibold flex items-center gap-2 mb-4">
            <Activity className="w-5 h-5 text-cyan-400" />
            Key Metrics
          </h2>

          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-3">
            {mockMetrics.map((metric, i) => (
              <MetricCard key={metric.id} metric={metric} index={i} />
            ))}
          </div>
        </section>

        {/* Main Content Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Alerts Panel */}
          <section className="lg:col-span-2">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold flex items-center gap-2">
                <AlertTriangle className="w-5 h-5 text-amber-400" />
                Active Alerts
                <span className="ml-2 px-2 py-0.5 rounded-full text-xs font-mono bg-amber-500/20 text-amber-400">
                  {mockAlerts.length}
                </span>
              </h2>
              <Link
                href="/admin/alerts"
                className="text-sm text-slate-500 hover:text-slate-300 transition-colors flex items-center gap-1"
              >
                View All <ChevronRight className="w-4 h-4" />
              </Link>
            </div>

            <div className="space-y-3">
              {mockAlerts.map((alert, i) => (
                <AlertItem key={alert.id} alert={alert} index={i} />
              ))}
            </div>
          </section>

          {/* Pending Actions */}
          <section>
            <h2 className="text-lg font-semibold flex items-center gap-2 mb-4">
              <Users className="w-5 h-5 text-cyan-400" />
              Pending Actions
            </h2>

            <div className="space-y-3">
              {mockPendingActions.map(action => (
                <PendingActionCard key={action.id} action={action} />
              ))}
            </div>
          </section>
        </div>

        {/* Terminal Logs */}
        <section className="mt-8">
          <TerminalLogs />
        </section>
      </div>
    </div>
  );
}

export default CommandCenterDashboard;
