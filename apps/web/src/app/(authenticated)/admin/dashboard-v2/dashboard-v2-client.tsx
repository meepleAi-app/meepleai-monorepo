'use client';

import React, { useState, useEffect } from 'react';
import {
  Users,
  Gamepad2,
  FileText,
  MessageSquare,
  TrendingUp,
  TrendingDown,
  Clock,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  Activity,
  Database,
  Cpu,
  Zap,
  Shield,
  Settings,
  BarChart3,
  PlusCircle,
  Search,
  Bell,
  ChevronRight,
  Sparkles,
  RefreshCw,
  Eye,
  UserPlus,
  FileUp,
  Bot,
  Server,
  HardDrive,
  Wifi,
  Crown,
} from 'lucide-react';
import { cn } from '@/lib/utils';

// ============================================================================
// TYPES
// ============================================================================

interface KpiData {
  label: string;
  value: string | number;
  trend?: number;
  trendLabel?: string;
  icon: React.ReactNode;
  color: 'amber' | 'emerald' | 'blue' | 'purple' | 'rose' | 'cyan';
  href?: string;
}

interface PendingItem {
  id: string;
  title: string;
  subtitle: string;
  type: 'game' | 'content' | 'user' | 'report';
  priority: 'high' | 'medium' | 'low';
  timestamp: string;
  avatar?: string;
}

interface ActivityItem {
  id: string;
  action: string;
  target: string;
  actor: string;
  timestamp: string;
  type: 'success' | 'warning' | 'info' | 'error';
}

interface SystemService {
  name: string;
  status: 'healthy' | 'degraded' | 'down';
  latency?: number;
  icon: React.ReactNode;
}

// ============================================================================
// MOCK DATA (Replace with real API calls)
// ============================================================================

const mockKpis: KpiData[] = [
  {
    label: 'Utenti Totali',
    value: '2,847',
    trend: 12,
    trendLabel: '+156 questo mese',
    icon: <Users className="w-6 h-6" />,
    color: 'amber',
    href: '/admin/users',
  },
  {
    label: 'Giochi Catalogo',
    value: '1,234',
    trend: 8,
    trendLabel: '+42 questa settimana',
    icon: <Gamepad2 className="w-6 h-6" />,
    color: 'emerald',
    href: '/admin/games',
  },
  {
    label: 'Proposte in Attesa',
    value: '18',
    trend: -5,
    trendLabel: '3 urgenti',
    icon: <FileText className="w-6 h-6" />,
    color: 'purple',
    href: '/admin/proposals',
  },
  {
    label: 'Chat AI Oggi',
    value: '892',
    trend: 23,
    trendLabel: '+180 vs ieri',
    icon: <MessageSquare className="w-6 h-6" />,
    color: 'blue',
    href: '/admin/analytics/ai',
  },
];

const mockPendingItems: PendingItem[] = [
  {
    id: '1',
    title: 'Everdell - Collector\'s Edition',
    subtitle: 'Proposta da Marco R.',
    type: 'game',
    priority: 'high',
    timestamp: '2 ore fa',
  },
  {
    id: '2',
    title: 'Regolamento Wingspan ITA',
    subtitle: 'PDF da verificare',
    type: 'content',
    priority: 'medium',
    timestamp: '5 ore fa',
  },
  {
    id: '3',
    title: 'Nuovo Editor richiesto',
    subtitle: 'Richiesta ruolo da Luca B.',
    type: 'user',
    priority: 'low',
    timestamp: 'Ieri',
  },
  {
    id: '4',
    title: 'Gloomhaven FAQ Update',
    subtitle: 'Contenuto segnalato',
    type: 'content',
    priority: 'medium',
    timestamp: 'Ieri',
  },
];

const mockActivity: ActivityItem[] = [
  {
    id: '1',
    action: 'ha approvato',
    target: 'Catan - Edizione 25° Anniversario',
    actor: 'Admin Laura',
    timestamp: '10 min fa',
    type: 'success',
  },
  {
    id: '2',
    action: 'ha segnalato contenuto in',
    target: 'Root - Regole Avanzate',
    actor: 'Sistema',
    timestamp: '25 min fa',
    type: 'warning',
  },
  {
    id: '3',
    action: 'nuovo utente registrato',
    target: 'giovanni.verdi@email.com',
    actor: 'Sistema',
    timestamp: '1 ora fa',
    type: 'info',
  },
  {
    id: '4',
    action: 'ha rifiutato proposta',
    target: 'Gioco duplicato: Ticket to Ride',
    actor: 'Admin Marco',
    timestamp: '2 ore fa',
    type: 'error',
  },
  {
    id: '5',
    action: 'backup completato',
    target: 'Database principale',
    actor: 'Sistema',
    timestamp: '3 ore fa',
    type: 'success',
  },
];

const mockSystemServices: SystemService[] = [
  { name: 'API Backend', status: 'healthy', latency: 45, icon: <Server className="w-4 h-4" /> },
  { name: 'Database', status: 'healthy', latency: 12, icon: <Database className="w-4 h-4" /> },
  { name: 'Redis Cache', status: 'healthy', latency: 2, icon: <Zap className="w-4 h-4" /> },
  { name: 'Qdrant Vector', status: 'healthy', latency: 28, icon: <HardDrive className="w-4 h-4" /> },
  { name: 'AI Service', status: 'degraded', latency: 230, icon: <Bot className="w-4 h-4" /> },
  { name: 'BGG Sync', status: 'healthy', latency: 180, icon: <Wifi className="w-4 h-4" /> },
];

// ============================================================================
// COMPONENTS
// ============================================================================

// KPI Card with glassmorphism and trend indicator
function KpiCard({ data }: { data: KpiData }) {
  const colorClasses = {
    amber: {
      bg: 'bg-amber-500/10 dark:bg-amber-500/20',
      icon: 'text-amber-600 dark:text-amber-400',
      border: 'border-amber-200/50 dark:border-amber-500/30',
      glow: 'group-hover:shadow-amber-500/20',
    },
    emerald: {
      bg: 'bg-emerald-500/10 dark:bg-emerald-500/20',
      icon: 'text-emerald-600 dark:text-emerald-400',
      border: 'border-emerald-200/50 dark:border-emerald-500/30',
      glow: 'group-hover:shadow-emerald-500/20',
    },
    blue: {
      bg: 'bg-blue-500/10 dark:bg-blue-500/20',
      icon: 'text-blue-600 dark:text-blue-400',
      border: 'border-blue-200/50 dark:border-blue-500/30',
      glow: 'group-hover:shadow-blue-500/20',
    },
    purple: {
      bg: 'bg-purple-500/10 dark:bg-purple-500/20',
      icon: 'text-purple-600 dark:text-purple-400',
      border: 'border-purple-200/50 dark:border-purple-500/30',
      glow: 'group-hover:shadow-purple-500/20',
    },
    rose: {
      bg: 'bg-rose-500/10 dark:bg-rose-500/20',
      icon: 'text-rose-600 dark:text-rose-400',
      border: 'border-rose-200/50 dark:border-rose-500/30',
      glow: 'group-hover:shadow-rose-500/20',
    },
    cyan: {
      bg: 'bg-cyan-500/10 dark:bg-cyan-500/20',
      icon: 'text-cyan-600 dark:text-cyan-400',
      border: 'border-cyan-200/50 dark:border-cyan-500/30',
      glow: 'group-hover:shadow-cyan-500/20',
    },
  };

  const colors = colorClasses[data.color];

  return (
    <a
      href={data.href}
      className={cn(
        'group relative flex flex-col p-5 rounded-2xl',
        'bg-white/70 dark:bg-zinc-900/70',
        'backdrop-blur-md border',
        colors.border,
        'transition-all duration-300 ease-out',
        'hover:-translate-y-1 hover:shadow-xl',
        colors.glow
      )}
    >
      {/* Decorative corner */}
      <div
        className={cn(
          'absolute -top-2 -right-2 w-16 h-16 rounded-full opacity-20',
          'transition-transform duration-300 group-hover:scale-150',
          data.color === 'amber' && 'bg-gradient-to-br from-amber-400 to-orange-500',
          data.color === 'emerald' && 'bg-gradient-to-br from-emerald-400 to-green-500',
          data.color === 'blue' && 'bg-gradient-to-br from-blue-400 to-indigo-500',
          data.color === 'purple' && 'bg-gradient-to-br from-purple-400 to-violet-500',
          data.color === 'rose' && 'bg-gradient-to-br from-rose-400 to-pink-500',
          data.color === 'cyan' && 'bg-gradient-to-br from-cyan-400 to-teal-500'
        )}
      />

      <div className="flex items-start justify-between mb-3">
        <div className={cn('p-2.5 rounded-xl', colors.bg)}>
          <div className={colors.icon}>{data.icon}</div>
        </div>

        {data.trend !== undefined && (
          <div
            className={cn(
              'flex items-center gap-1 text-xs font-medium px-2 py-1 rounded-full',
              data.trend >= 0
                ? 'text-emerald-700 bg-emerald-100 dark:text-emerald-300 dark:bg-emerald-900/50'
                : 'text-rose-700 bg-rose-100 dark:text-rose-300 dark:bg-rose-900/50'
            )}
          >
            {data.trend >= 0 ? (
              <TrendingUp className="w-3 h-3" />
            ) : (
              <TrendingDown className="w-3 h-3" />
            )}
            {Math.abs(data.trend)}%
          </div>
        )}
      </div>

      <div className="mt-auto">
        <p className="text-3xl font-bold font-quicksand text-zinc-900 dark:text-white">
          {data.value}
        </p>
        <p className="text-sm text-zinc-500 dark:text-zinc-400 mt-1">{data.label}</p>
        {data.trendLabel && (
          <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-1">{data.trendLabel}</p>
        )}
      </div>

      <ChevronRight
        className={cn(
          'absolute bottom-4 right-4 w-5 h-5',
          'text-zinc-300 dark:text-zinc-600',
          'transition-all duration-300',
          'group-hover:text-zinc-500 group-hover:translate-x-1'
        )}
      />
    </a>
  );
}

// Pending Item Card
function PendingItemCard({ item }: { item: PendingItem }) {
  const typeConfig = {
    game: {
      icon: <Gamepad2 className="w-4 h-4" />,
      bg: 'bg-amber-100 dark:bg-amber-900/30',
      text: 'text-amber-700 dark:text-amber-300',
    },
    content: {
      icon: <FileText className="w-4 h-4" />,
      bg: 'bg-blue-100 dark:bg-blue-900/30',
      text: 'text-blue-700 dark:text-blue-300',
    },
    user: {
      icon: <UserPlus className="w-4 h-4" />,
      bg: 'bg-purple-100 dark:bg-purple-900/30',
      text: 'text-purple-700 dark:text-purple-300',
    },
    report: {
      icon: <AlertTriangle className="w-4 h-4" />,
      bg: 'bg-rose-100 dark:bg-rose-900/30',
      text: 'text-rose-700 dark:text-rose-300',
    },
  };

  const priorityConfig = {
    high: 'border-l-rose-500',
    medium: 'border-l-amber-500',
    low: 'border-l-zinc-300 dark:border-l-zinc-600',
  };

  const config = typeConfig[item.type];

  return (
    <div
      className={cn(
        'flex items-center gap-4 p-4 rounded-xl',
        'bg-white/50 dark:bg-zinc-800/50',
        'border-l-4',
        priorityConfig[item.priority],
        'hover:bg-white dark:hover:bg-zinc-800',
        'transition-colors duration-200 cursor-pointer'
      )}
    >
      <div className={cn('p-2 rounded-lg', config.bg, config.text)}>{config.icon}</div>

      <div className="flex-1 min-w-0">
        <p className="font-medium text-zinc-900 dark:text-white truncate">{item.title}</p>
        <p className="text-sm text-zinc-500 dark:text-zinc-400 truncate">{item.subtitle}</p>
      </div>

      <div className="text-xs text-zinc-400 dark:text-zinc-500 whitespace-nowrap">
        {item.timestamp}
      </div>
    </div>
  );
}

// Activity Item
function ActivityItemRow({ item }: { item: ActivityItem }) {
  const typeConfig = {
    success: {
      icon: <CheckCircle2 className="w-4 h-4" />,
      color: 'text-emerald-500',
      bg: 'bg-emerald-100 dark:bg-emerald-900/30',
    },
    warning: {
      icon: <AlertTriangle className="w-4 h-4" />,
      color: 'text-amber-500',
      bg: 'bg-amber-100 dark:bg-amber-900/30',
    },
    info: {
      icon: <Activity className="w-4 h-4" />,
      color: 'text-blue-500',
      bg: 'bg-blue-100 dark:bg-blue-900/30',
    },
    error: {
      icon: <XCircle className="w-4 h-4" />,
      color: 'text-rose-500',
      bg: 'bg-rose-100 dark:bg-rose-900/30',
    },
  };

  const config = typeConfig[item.type];

  return (
    <div className="flex items-start gap-3 py-3">
      <div className={cn('p-1.5 rounded-lg mt-0.5', config.bg, config.color)}>{config.icon}</div>

      <div className="flex-1 min-w-0">
        <p className="text-sm text-zinc-700 dark:text-zinc-300">
          <span className="font-medium text-zinc-900 dark:text-white">{item.actor}</span>{' '}
          {item.action}{' '}
          <span className="font-medium text-zinc-900 dark:text-white">{item.target}</span>
        </p>
        <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-0.5">{item.timestamp}</p>
      </div>
    </div>
  );
}

// System Status Badge
function SystemStatusBadge({ service }: { service: SystemService }) {
  const statusConfig = {
    healthy: {
      color: 'text-emerald-600 dark:text-emerald-400',
      bg: 'bg-emerald-100 dark:bg-emerald-900/30',
      dot: 'bg-emerald-500',
    },
    degraded: {
      color: 'text-amber-600 dark:text-amber-400',
      bg: 'bg-amber-100 dark:bg-amber-900/30',
      dot: 'bg-amber-500',
    },
    down: {
      color: 'text-rose-600 dark:text-rose-400',
      bg: 'bg-rose-100 dark:bg-rose-900/30',
      dot: 'bg-rose-500',
    },
  };

  const config = statusConfig[service.status];

  return (
    <div
      className={cn(
        'flex items-center gap-2 px-3 py-2 rounded-lg',
        'bg-white/50 dark:bg-zinc-800/50',
        'border border-zinc-200/50 dark:border-zinc-700/50'
      )}
    >
      <div className={cn('p-1 rounded', config.bg, config.color)}>{service.icon}</div>
      <span className="text-sm font-medium text-zinc-700 dark:text-zinc-300">{service.name}</span>
      <div className="flex-1" />
      {service.latency && (
        <span className="text-xs text-zinc-400 dark:text-zinc-500">{service.latency}ms</span>
      )}
      <div className={cn('w-2 h-2 rounded-full animate-pulse', config.dot)} />
    </div>
  );
}

// Quick Action Button
function QuickActionButton({
  icon,
  label,
  variant = 'default',
  badge,
  onClick,
}: {
  icon: React.ReactNode;
  label: string;
  variant?: 'default' | 'primary';
  badge?: number;
  onClick?: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'relative flex flex-col items-center justify-center gap-2 p-4 rounded-xl',
        'transition-all duration-200',
        variant === 'primary'
          ? 'bg-gradient-to-br from-amber-500 to-orange-600 text-white shadow-lg shadow-amber-500/25 hover:shadow-xl hover:shadow-amber-500/30 hover:-translate-y-0.5'
          : 'bg-white/70 dark:bg-zinc-800/70 text-zinc-700 dark:text-zinc-300 hover:bg-white dark:hover:bg-zinc-800 border border-zinc-200/50 dark:border-zinc-700/50'
      )}
    >
      {badge !== undefined && badge > 0 && (
        <span className="absolute -top-1 -right-1 w-5 h-5 flex items-center justify-center text-xs font-bold bg-rose-500 text-white rounded-full">
          {badge > 9 ? '9+' : badge}
        </span>
      )}
      {icon}
      <span className="text-xs font-medium">{label}</span>
    </button>
  );
}

// ============================================================================
// MAIN DASHBOARD COMPONENT
// ============================================================================

export default function AdminDashboardV2Client() {
  const [currentTime, setCurrentTime] = useState(new Date());
  const [isRefreshing, setIsRefreshing] = useState(false);

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 60000);
    return () => clearInterval(timer);
  }, []);

  const handleRefresh = () => {
    setIsRefreshing(true);
    setTimeout(() => setIsRefreshing(false), 1000);
  };

  const greeting = () => {
    const hour = currentTime.getHours();
    if (hour < 12) return 'Buongiorno';
    if (hour < 18) return 'Buon pomeriggio';
    return 'Buonasera';
  };

  return (
    <div className="min-h-full -mx-4 sm:-mx-6 lg:-mx-8 -my-4 bg-gradient-to-br from-amber-50/50 via-white to-orange-50/30 dark:from-zinc-950 dark:via-zinc-900 dark:to-zinc-950">
      {/* Decorative background elements */}
      <div className="absolute inset-0 pointer-events-none overflow-hidden">
        <div className="absolute -top-40 -right-40 w-96 h-96 bg-gradient-to-br from-amber-200/30 to-orange-300/20 dark:from-amber-900/20 dark:to-orange-900/10 rounded-full blur-3xl" />
        <div className="absolute top-1/2 -left-40 w-80 h-80 bg-gradient-to-br from-purple-200/20 to-violet-300/10 dark:from-purple-900/10 dark:to-violet-900/5 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 right-1/3 w-72 h-72 bg-gradient-to-br from-emerald-200/20 to-teal-300/10 dark:from-emerald-900/10 dark:to-teal-900/5 rounded-full blur-3xl" />
      </div>

      <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Header */}
        <header className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
          <div>
            <div className="flex items-center gap-3 mb-1">
              <div className="p-2 bg-gradient-to-br from-amber-500 to-orange-600 rounded-xl shadow-lg shadow-amber-500/25">
                <Crown className="w-5 h-5 text-white" />
              </div>
              <h1 className="text-2xl sm:text-3xl font-bold font-quicksand text-zinc-900 dark:text-white">
                {greeting()}, Admin!
              </h1>
            </div>
            <p className="text-sm text-zinc-500 dark:text-zinc-400 flex items-center gap-2">
              <Clock className="w-4 h-4" />
              Ultimo aggiornamento:{' '}
              {currentTime.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={handleRefresh}
              className={cn(
                'p-2.5 rounded-xl bg-white/70 dark:bg-zinc-800/70 border border-zinc-200/50 dark:border-zinc-700/50',
                'hover:bg-white dark:hover:bg-zinc-800 transition-colors'
              )}
            >
              <RefreshCw
                className={cn('w-5 h-5 text-zinc-600 dark:text-zinc-400', isRefreshing && 'animate-spin')}
              />
            </button>
            <button className="relative p-2.5 rounded-xl bg-white/70 dark:bg-zinc-800/70 border border-zinc-200/50 dark:border-zinc-700/50 hover:bg-white dark:hover:bg-zinc-800 transition-colors">
              <Bell className="w-5 h-5 text-zinc-600 dark:text-zinc-400" />
              <span className="absolute -top-1 -right-1 w-4 h-4 flex items-center justify-center text-[10px] font-bold bg-rose-500 text-white rounded-full">
                3
              </span>
            </button>
          </div>
        </header>

        {/* KPI Cards Grid */}
        <section className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          {mockKpis.map((kpi, index) => (
            <KpiCard key={index} data={kpi} />
          ))}
        </section>

        {/* Main Content Grid */}
        <div className="grid lg:grid-cols-3 gap-6">
          {/* Left Column - Pending Items */}
          <div className="lg:col-span-2 space-y-6">
            {/* Pending Approvals */}
            <section
              className={cn(
                'p-6 rounded-2xl',
                'bg-white/70 dark:bg-zinc-900/70',
                'backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50'
              )}
            >
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
                    <FileText className="w-5 h-5 text-purple-600 dark:text-purple-400" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold font-quicksand text-zinc-900 dark:text-white">
                      In Attesa di Revisione
                    </h2>
                    <p className="text-sm text-zinc-500 dark:text-zinc-400">
                      {mockPendingItems.length} elementi richiedono attenzione
                    </p>
                  </div>
                </div>
                <a
                  href="/admin/proposals"
                  className="text-sm font-medium text-amber-600 dark:text-amber-400 hover:underline flex items-center gap-1"
                >
                  Vedi tutti
                  <ChevronRight className="w-4 h-4" />
                </a>
              </div>

              <div className="space-y-3">
                {mockPendingItems.map((item) => (
                  <PendingItemCard key={item.id} item={item} />
                ))}
              </div>
            </section>

            {/* System Status */}
            <section
              className={cn(
                'p-6 rounded-2xl',
                'bg-white/70 dark:bg-zinc-900/70',
                'backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50'
              )}
            >
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-emerald-100 dark:bg-emerald-900/30 rounded-lg">
                    <Activity className="w-5 h-5 text-emerald-600 dark:text-emerald-400" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold font-quicksand text-zinc-900 dark:text-white">
                      Stato Sistema
                    </h2>
                    <p className="text-sm text-zinc-500 dark:text-zinc-400">
                      5/6 servizi operativi
                    </p>
                  </div>
                </div>
                <a
                  href="/admin/system"
                  className="text-sm font-medium text-amber-600 dark:text-amber-400 hover:underline flex items-center gap-1"
                >
                  Dettagli
                  <ChevronRight className="w-4 h-4" />
                </a>
              </div>

              <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                {mockSystemServices.map((service) => (
                  <SystemStatusBadge key={service.name} service={service} />
                ))}
              </div>
            </section>
          </div>

          {/* Right Column - Activity & Quick Actions */}
          <div className="space-y-6">
            {/* Quick Actions */}
            <section
              className={cn(
                'p-6 rounded-2xl',
                'bg-white/70 dark:bg-zinc-900/70',
                'backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50'
              )}
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="p-2 bg-amber-100 dark:bg-amber-900/30 rounded-lg">
                  <Zap className="w-5 h-5 text-amber-600 dark:text-amber-400" />
                </div>
                <h2 className="text-lg font-semibold font-quicksand text-zinc-900 dark:text-white">
                  Azioni Rapide
                </h2>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <QuickActionButton
                  icon={<PlusCircle className="w-5 h-5" />}
                  label="Aggiungi"
                  variant="primary"
                />
                <QuickActionButton
                  icon={<Eye className="w-5 h-5" />}
                  label="Revisiona"
                  badge={3}
                />
                <QuickActionButton icon={<Search className="w-5 h-5" />} label="Cerca" />
                <QuickActionButton icon={<FileUp className="w-5 h-5" />} label="Import" />
                <QuickActionButton icon={<BarChart3 className="w-5 h-5" />} label="Analytics" />
                <QuickActionButton icon={<Settings className="w-5 h-5" />} label="Config" />
              </div>
            </section>

            {/* Recent Activity */}
            <section
              className={cn(
                'p-6 rounded-2xl',
                'bg-white/70 dark:bg-zinc-900/70',
                'backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50'
              )}
            >
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
                    <Clock className="w-5 h-5 text-blue-600 dark:text-blue-400" />
                  </div>
                  <h2 className="text-lg font-semibold font-quicksand text-zinc-900 dark:text-white">
                    Attività Recente
                  </h2>
                </div>
                <a
                  href="/admin/activity"
                  className="text-sm font-medium text-amber-600 dark:text-amber-400 hover:underline"
                >
                  Cronologia
                </a>
              </div>

              <div className="divide-y divide-zinc-200/50 dark:divide-zinc-700/50">
                {mockActivity.map((item) => (
                  <ActivityItemRow key={item.id} item={item} />
                ))}
              </div>
            </section>

            {/* AI Insights Banner */}
            <section
              className={cn(
                'p-5 rounded-2xl',
                'bg-gradient-to-br from-violet-500/10 via-purple-500/10 to-fuchsia-500/10',
                'dark:from-violet-500/20 dark:via-purple-500/20 dark:to-fuchsia-500/20',
                'border border-purple-200/50 dark:border-purple-500/30'
              )}
            >
              <div className="flex items-start gap-3">
                <div className="p-2 bg-purple-500/20 rounded-lg">
                  <Sparkles className="w-5 h-5 text-purple-600 dark:text-purple-400" />
                </div>
                <div className="flex-1">
                  <h3 className="font-semibold text-purple-900 dark:text-purple-100 mb-1">
                    Suggerimento AI
                  </h3>
                  <p className="text-sm text-purple-700 dark:text-purple-300">
                    Ci sono 5 giochi popolari su BGG che potrebbero interessare il catalogo. Vuoi
                    importarli?
                  </p>
                  <button className="mt-3 text-sm font-medium text-purple-600 dark:text-purple-400 hover:underline flex items-center gap-1">
                    Esplora suggerimenti
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </section>
          </div>
        </div>

        {/* Bottom Action Bar (Mobile-first) */}
        <div className="fixed bottom-0 left-0 right-0 lg:hidden p-4 bg-white/80 dark:bg-zinc-900/80 backdrop-blur-xl border-t border-zinc-200/50 dark:border-zinc-700/50">
          <div className="flex items-center justify-around max-w-lg mx-auto">
            <button className="flex flex-col items-center gap-1 text-amber-600">
              <PlusCircle className="w-6 h-6" />
              <span className="text-xs font-medium">Aggiungi</span>
            </button>
            <button className="flex flex-col items-center gap-1 text-zinc-500 relative">
              <Eye className="w-6 h-6" />
              <span className="text-xs font-medium">Revisiona</span>
              <span className="absolute -top-1 right-0 w-4 h-4 flex items-center justify-center text-[10px] font-bold bg-rose-500 text-white rounded-full">
                3
              </span>
            </button>
            <button className="flex flex-col items-center gap-1 text-zinc-500">
              <Search className="w-6 h-6" />
              <span className="text-xs font-medium">Cerca</span>
            </button>
            <button className="flex flex-col items-center gap-1 text-zinc-500">
              <BarChart3 className="w-6 h-6" />
              <span className="text-xs font-medium">Analytics</span>
            </button>
          </div>
        </div>

        {/* Spacer for mobile bottom bar */}
        <div className="h-24 lg:hidden" />
      </div>
    </div>
  );
}
