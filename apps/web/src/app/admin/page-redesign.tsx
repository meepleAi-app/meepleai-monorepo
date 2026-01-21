/**
 * Admin Dashboard - Enhanced Redesign
 *
 * A warm, playful yet professional admin dashboard for MeepleAI.
 * Features board game aesthetic with modern monitoring capabilities.
 *
 * Design: Warm Industrial Playfulness
 * - Amber/orange accents (MeepleAI brand)
 * - Subtle board game motifs
 * - Dark mode compatible
 * - Italian language UI
 */

'use client';

import React, { useState, useEffect } from 'react';
import {
  Users,
  Gamepad2,
  Activity,
  Zap,
  Database,
  Server,
  Cpu,
  HardDrive,
  CheckCircle2,
  XCircle,
  Clock,
  TrendingUp,
  TrendingDown,
  AlertTriangle,
  Settings,
  Download,
  Trash2,
  UserCog,
  ShieldCheck,
  Eye,
  Sparkles,
  Bot,
  CircleDot,
  ArrowUpRight,
  RefreshCw,
} from 'lucide-react';

import { DashboardHeader } from '@/components/admin';

// ============================================================================
// Types
// ============================================================================

interface KPICard {
  title: string;
  value: string | number;
  trend?: number;
  trendLabel?: string;
  icon: React.ReactNode;
  badge?: string;
  badgeVariant?: 'warning' | 'success' | 'error';
  subtitle?: string;
}

interface ServiceStatus {
  name: string;
  status: 'healthy' | 'degraded' | 'down';
  latency?: number;
  icon: React.ReactNode;
}

interface ActivityItem {
  id: string;
  action: string;
  user: string;
  timestamp: Date;
  type: 'user' | 'game' | 'system' | 'ai';
}

interface PendingApproval {
  id: string;
  title: string;
  submittedBy: string;
  submittedAt: Date;
}

// ============================================================================
// Mock Data
// ============================================================================

const mockKPIs: KPICard[] = [
  {
    title: 'Utenti Totali',
    value: '2,847',
    trend: 12.5,
    trendLabel: 'vs mese scorso',
    icon: <Users className="h-5 w-5" />,
  },
  {
    title: 'Sessioni Attive',
    value: '156',
    icon: <Activity className="h-5 w-5" />,
    subtitle: 'in tempo reale',
  },
  {
    title: 'Giochi in Catalogo',
    value: '1,234',
    trend: 8,
    trendLabel: 'nuovi questa settimana',
    icon: <Gamepad2 className="h-5 w-5" />,
    badge: '12 in attesa',
    badgeVariant: 'warning',
  },
  {
    title: 'Richieste AI Oggi',
    value: '8,492',
    icon: <Zap className="h-5 w-5" />,
    subtitle: '~€24.50 stimati',
  },
];

const mockServices: ServiceStatus[] = [
  { name: 'PostgreSQL', status: 'healthy', latency: 12, icon: <Database className="h-4 w-4" /> },
  { name: 'Redis Cache', status: 'healthy', latency: 2, icon: <HardDrive className="h-4 w-4" /> },
  { name: 'Qdrant Vector', status: 'healthy', latency: 45, icon: <CircleDot className="h-4 w-4" /> },
  { name: 'API Backend', status: 'healthy', latency: 89, icon: <Server className="h-4 w-4" /> },
  { name: 'Embedding Service', status: 'degraded', latency: 234, icon: <Cpu className="h-4 w-4" /> },
  { name: 'Reranker Service', status: 'healthy', latency: 156, icon: <Bot className="h-4 w-4" /> },
];

const mockActivity: ActivityItem[] = [
  { id: '1', action: 'Nuovo utente registrato', user: 'mario.rossi@email.com', timestamp: new Date(Date.now() - 5 * 60000), type: 'user' },
  { id: '2', action: 'Gioco approvato', user: 'Admin', timestamp: new Date(Date.now() - 12 * 60000), type: 'game' },
  { id: '3', action: 'Cache invalidata', user: 'Sistema', timestamp: new Date(Date.now() - 25 * 60000), type: 'system' },
  { id: '4', action: 'Prompt attivato v2.3', user: 'Admin', timestamp: new Date(Date.now() - 45 * 60000), type: 'ai' },
  { id: '5', action: 'PDF processato', user: 'luigi.verdi@email.com', timestamp: new Date(Date.now() - 60 * 60000), type: 'game' },
];

const mockPendingApprovals: PendingApproval[] = [
  { id: '1', title: 'Terraforming Mars: Ares Expedition', submittedBy: 'editor_paolo', submittedAt: new Date(Date.now() - 2 * 3600000) },
  { id: '2', title: 'Wingspan: Oceania', submittedBy: 'editor_maria', submittedAt: new Date(Date.now() - 5 * 3600000) },
  { id: '3', title: 'Root: I Sotterranei', submittedBy: 'editor_paolo', submittedAt: new Date(Date.now() - 8 * 3600000) },
];

// ============================================================================
// Utility Components
// ============================================================================

function MeepleDecoration({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 32" className={className} fill="currentColor">
      <path d="M12 0C7.5 0 4 3 4 7c0 2.5 1.5 4.5 3.5 5.5L6 32h12l-1.5-19.5C18.5 11.5 20 9.5 20 7c0-4-3.5-7-8-7z" />
    </svg>
  );
}

function DicePattern({ className }: { className?: string }) {
  return (
    <div className={`absolute opacity-[0.03] pointer-events-none ${className}`}>
      <svg width="60" height="60" viewBox="0 0 60 60" fill="currentColor">
        <circle cx="10" cy="10" r="3" />
        <circle cx="30" cy="10" r="3" />
        <circle cx="50" cy="10" r="3" />
        <circle cx="10" cy="30" r="3" />
        <circle cx="30" cy="30" r="3" />
        <circle cx="50" cy="30" r="3" />
        <circle cx="10" cy="50" r="3" />
        <circle cx="30" cy="50" r="3" />
        <circle cx="50" cy="50" r="3" />
      </svg>
    </div>
  );
}

// ============================================================================
// Sub-Components
// ============================================================================

function KPICardsGrid({ cards }: { cards: KPICard[] }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map((card, index) => (
        <div
          key={index}
          className="group relative overflow-hidden rounded-xl border border-stone-200 bg-white p-5 shadow-sm transition-all hover:border-orange-200 hover:shadow-md dark:border-stone-800 dark:bg-stone-900"
          style={{ animationDelay: `${index * 100}ms` }}
        >
          {/* Decorative corner */}
          <div className="absolute -right-6 -top-6 h-16 w-16 rounded-full bg-gradient-to-br from-orange-500/10 to-amber-500/10 transition-transform group-hover:scale-150" />

          <div className="relative">
            <div className="mb-3 flex items-center justify-between">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-orange-100 text-orange-600 dark:bg-orange-500/20 dark:text-orange-400">
                {card.icon}
              </div>
              {card.badge && (
                <span className={`rounded-full px-2 py-1 text-xs font-medium ${
                  card.badgeVariant === 'warning'
                    ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400'
                    : card.badgeVariant === 'error'
                    ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                    : 'bg-green-100 text-green-700 dark:bg-green-500/20 dark:text-green-400'
                }`}>
                  {card.badge}
                </span>
              )}
            </div>

            <p className="text-sm font-medium text-stone-500 dark:text-stone-400">{card.title}</p>
            <p className="mt-1 font-quicksand text-3xl font-bold text-stone-900 dark:text-white">{card.value}</p>

            {card.trend !== undefined && (
              <div className="mt-2 flex items-center gap-1 text-sm">
                {card.trend > 0 ? (
                  <TrendingUp className="h-4 w-4 text-green-500" />
                ) : (
                  <TrendingDown className="h-4 w-4 text-red-500" />
                )}
                <span className={card.trend > 0 ? 'text-green-600' : 'text-red-600'}>
                  {card.trend > 0 ? '+' : ''}{card.trend}%
                </span>
                <span className="text-stone-400">{card.trendLabel}</span>
              </div>
            )}

            {card.subtitle && (
              <p className="mt-2 text-sm text-stone-400">{card.subtitle}</p>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

function SystemHealthMatrix({ services }: { services: ServiceStatus[] }) {
  const getStatusColor = (status: ServiceStatus['status']) => {
    switch (status) {
      case 'healthy': return 'bg-green-500';
      case 'degraded': return 'bg-amber-500';
      case 'down': return 'bg-red-500';
    }
  };

  const getStatusBg = (status: ServiceStatus['status']) => {
    switch (status) {
      case 'healthy': return 'bg-green-50 border-green-200 dark:bg-green-500/10 dark:border-green-500/30';
      case 'degraded': return 'bg-amber-50 border-amber-200 dark:bg-amber-500/10 dark:border-amber-500/30';
      case 'down': return 'bg-red-50 border-red-200 dark:bg-red-500/10 dark:border-red-500/30';
    }
  };

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <div className="mb-4 flex items-center justify-between">
        <h3 className="font-quicksand text-lg font-semibold text-stone-900 dark:text-white">
          Stato Servizi
        </h3>
        <button className="flex items-center gap-1 text-sm text-orange-600 hover:text-orange-700">
          <RefreshCw className="h-4 w-4" />
          Aggiorna
        </button>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {services.map((service, index) => (
          <div
            key={index}
            className={`relative overflow-hidden rounded-lg border p-3 transition-all hover:shadow-sm ${getStatusBg(service.status)}`}
          >
            <div className="flex items-center gap-2">
              <div className={`h-2 w-2 rounded-full ${getStatusColor(service.status)} animate-pulse`} />
              <span className="text-stone-600 dark:text-stone-400">{service.icon}</span>
            </div>
            <p className="mt-2 text-sm font-medium text-stone-900 dark:text-white">{service.name}</p>
            {service.latency && (
              <p className="text-xs text-stone-500">{service.latency}ms</p>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function ActivityTimeline({ activities }: { activities: ActivityItem[] }) {
  const getActivityIcon = (type: ActivityItem['type']) => {
    switch (type) {
      case 'user': return <Users className="h-4 w-4" />;
      case 'game': return <Gamepad2 className="h-4 w-4" />;
      case 'system': return <Settings className="h-4 w-4" />;
      case 'ai': return <Sparkles className="h-4 w-4" />;
    }
  };

  const getActivityColor = (type: ActivityItem['type']) => {
    switch (type) {
      case 'user': return 'bg-blue-100 text-blue-600 dark:bg-blue-500/20 dark:text-blue-400';
      case 'game': return 'bg-green-100 text-green-600 dark:bg-green-500/20 dark:text-green-400';
      case 'system': return 'bg-stone-100 text-stone-600 dark:bg-stone-500/20 dark:text-stone-400';
      case 'ai': return 'bg-purple-100 text-purple-600 dark:bg-purple-500/20 dark:text-purple-400';
    }
  };

  const formatTimeAgo = (date: Date) => {
    const minutes = Math.floor((Date.now() - date.getTime()) / 60000);
    if (minutes < 60) return `${minutes}m fa`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h fa`;
    return `${Math.floor(hours / 24)}g fa`;
  };

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <div className="mb-4 flex items-center justify-between">
        <h3 className="font-quicksand text-lg font-semibold text-stone-900 dark:text-white">
          Attività Recenti
        </h3>
        <button className="text-sm text-orange-600 hover:text-orange-700">
          Vedi tutto
        </button>
      </div>

      <div className="space-y-3">
        {activities.map((activity, index) => (
          <div
            key={activity.id}
            className="flex items-start gap-3 rounded-lg p-2 transition-colors hover:bg-stone-50 dark:hover:bg-stone-800/50"
            style={{ animationDelay: `${index * 50}ms` }}
          >
            <div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${getActivityColor(activity.type)}`}>
              {getActivityIcon(activity.type)}
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-stone-900 dark:text-white">{activity.action}</p>
              <p className="truncate text-xs text-stone-500">{activity.user}</p>
            </div>
            <div className="flex items-center gap-1 text-xs text-stone-400">
              <Clock className="h-3 w-3" />
              {formatTimeAgo(activity.timestamp)}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function QuickActionsPanel() {
  const actions = [
    { label: 'Approva Giochi', icon: <CheckCircle2 className="h-5 w-5" />, badge: 12, color: 'from-green-500 to-emerald-600' },
    { label: 'Gestisci Utenti', icon: <UserCog className="h-5 w-5" />, color: 'from-blue-500 to-indigo-600' },
    { label: 'Vedi Alert', icon: <AlertTriangle className="h-5 w-5" />, badge: 3, color: 'from-amber-500 to-orange-600' },
    { label: 'Svuota Cache', icon: <Trash2 className="h-5 w-5" />, color: 'from-red-500 to-rose-600' },
    { label: 'Esporta Dati', icon: <Download className="h-5 w-5" />, color: 'from-purple-500 to-violet-600' },
    { label: 'Configurazione', icon: <Settings className="h-5 w-5" />, color: 'from-stone-500 to-stone-700' },
  ];

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 font-quicksand text-lg font-semibold text-stone-900 dark:text-white">
        Azioni Rapide
      </h3>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {actions.map((action, index) => (
          <button
            key={index}
            className="group relative flex flex-col items-center justify-center gap-2 rounded-xl border border-stone-200 bg-stone-50 p-4 transition-all hover:border-orange-200 hover:shadow-md dark:border-stone-700 dark:bg-stone-800 dark:hover:border-orange-500/50"
          >
            <div className={`flex h-10 w-10 items-center justify-center rounded-lg bg-gradient-to-br ${action.color} text-white shadow-lg transition-transform group-hover:scale-110`}>
              {action.icon}
            </div>
            <span className="text-center text-xs font-medium text-stone-700 dark:text-stone-300">
              {action.label}
            </span>
            {action.badge && (
              <span className="absolute -right-1 -top-1 flex h-5 w-5 items-center justify-center rounded-full bg-orange-500 text-[10px] font-bold text-white">
                {action.badge}
              </span>
            )}
          </button>
        ))}
      </div>
    </div>
  );
}

function PendingApprovalsWidget({ approvals }: { approvals: PendingApproval[] }) {
  const formatTimeAgo = (date: Date) => {
    const hours = Math.floor((Date.now() - date.getTime()) / 3600000);
    if (hours < 24) return `${hours}h fa`;
    return `${Math.floor(hours / 24)}g fa`;
  };

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <div className="mb-4 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h3 className="font-quicksand text-lg font-semibold text-stone-900 dark:text-white">
            In Attesa di Approvazione
          </h3>
          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-amber-100 text-xs font-bold text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
            {approvals.length}
          </span>
        </div>
        <button className="flex items-center gap-1 text-sm text-orange-600 hover:text-orange-700">
          Vedi tutti
          <ArrowUpRight className="h-4 w-4" />
        </button>
      </div>

      <div className="space-y-3">
        {approvals.map((approval) => (
          <div
            key={approval.id}
            className="flex items-center justify-between rounded-lg border border-stone-100 bg-stone-50 p-3 dark:border-stone-800 dark:bg-stone-800/50"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-stone-900 dark:text-white">
                {approval.title}
              </p>
              <p className="text-xs text-stone-500">
                da {approval.submittedBy} · {formatTimeAgo(approval.submittedAt)}
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button className="flex h-8 w-8 items-center justify-center rounded-lg bg-green-100 text-green-600 transition-colors hover:bg-green-200 dark:bg-green-500/20 dark:text-green-400 dark:hover:bg-green-500/30">
                <CheckCircle2 className="h-4 w-4" />
              </button>
              <button className="flex h-8 w-8 items-center justify-center rounded-lg bg-red-100 text-red-600 transition-colors hover:bg-red-200 dark:bg-red-500/20 dark:text-red-400 dark:hover:bg-red-500/30">
                <XCircle className="h-4 w-4" />
              </button>
              <button className="flex h-8 w-8 items-center justify-center rounded-lg text-stone-400 transition-colors hover:bg-stone-200 hover:text-stone-600 dark:hover:bg-stone-700">
                <Eye className="h-4 w-4" />
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ChartsSection() {
  // Mock chart data
  const days = ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom'];
  const requestsData = [4200, 5100, 4800, 6200, 7500, 3200, 8492];
  const maxRequests = Math.max(...requestsData);

  const aiUsage = [
    { model: 'Claude', tokens: 45, color: 'bg-orange-500' },
    { model: 'GPT-4', tokens: 30, color: 'bg-blue-500' },
    { model: 'Local', tokens: 25, color: 'bg-green-500' },
  ];

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      {/* API Requests Chart */}
      <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <h3 className="mb-4 font-quicksand text-lg font-semibold text-stone-900 dark:text-white">
          Richieste API - Ultimi 7 Giorni
        </h3>

        <div className="flex h-48 items-end justify-between gap-2">
          {requestsData.map((value, index) => (
            <div key={index} className="flex flex-1 flex-col items-center gap-2">
              <div
                className="w-full rounded-t-lg bg-gradient-to-t from-orange-500 to-amber-400 transition-all hover:from-orange-600 hover:to-amber-500"
                style={{ height: `${(value / maxRequests) * 100}%` }}
              />
              <span className="text-xs text-stone-500">{days[index]}</span>
            </div>
          ))}
        </div>

        <div className="mt-4 flex items-center justify-between border-t border-stone-100 pt-4 dark:border-stone-800">
          <div>
            <p className="text-sm text-stone-500">Totale Settimana</p>
            <p className="font-quicksand text-xl font-bold text-stone-900 dark:text-white">
              39,492
            </p>
          </div>
          <div className="flex items-center gap-1 text-sm text-green-600">
            <TrendingUp className="h-4 w-4" />
            +18% vs settimana scorsa
          </div>
        </div>
      </div>

      {/* AI Usage Donut */}
      <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <h3 className="mb-4 font-quicksand text-lg font-semibold text-stone-900 dark:text-white">
          Utilizzo AI per Modello
        </h3>

        <div className="flex items-center justify-center gap-8">
          {/* Simple donut representation */}
          <div className="relative h-36 w-36">
            <svg viewBox="0 0 36 36" className="h-full w-full -rotate-90">
              <circle
                cx="18"
                cy="18"
                r="15.915"
                fill="none"
                stroke="currentColor"
                strokeWidth="3"
                className="text-stone-100 dark:text-stone-800"
              />
              {/* Claude segment */}
              <circle
                cx="18"
                cy="18"
                r="15.915"
                fill="none"
                stroke="currentColor"
                strokeWidth="3"
                strokeDasharray="45 55"
                strokeDashoffset="0"
                className="text-orange-500"
              />
              {/* GPT segment */}
              <circle
                cx="18"
                cy="18"
                r="15.915"
                fill="none"
                stroke="currentColor"
                strokeWidth="3"
                strokeDasharray="30 70"
                strokeDashoffset="-45"
                className="text-blue-500"
              />
              {/* Local segment */}
              <circle
                cx="18"
                cy="18"
                r="15.915"
                fill="none"
                stroke="currentColor"
                strokeWidth="3"
                strokeDasharray="25 75"
                strokeDashoffset="-75"
                className="text-green-500"
              />
            </svg>
            <div className="absolute inset-0 flex flex-col items-center justify-center">
              <p className="font-quicksand text-2xl font-bold text-stone-900 dark:text-white">2.1M</p>
              <p className="text-xs text-stone-500">tokens oggi</p>
            </div>
          </div>

          {/* Legend */}
          <div className="space-y-3">
            {aiUsage.map((item) => (
              <div key={item.model} className="flex items-center gap-3">
                <div className={`h-3 w-3 rounded-full ${item.color}`} />
                <div>
                  <p className="text-sm font-medium text-stone-900 dark:text-white">{item.model}</p>
                  <p className="text-xs text-stone-500">{item.tokens}%</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function AlertsBanner() {
  const criticalCount = 2;
  const healthyServices = 5;
  const totalServices = 6;
  const hasIssues = criticalCount > 0 || healthyServices < totalServices;

  return (
    <div className={`rounded-xl p-4 ${
      hasIssues
        ? 'border border-amber-200 bg-gradient-to-r from-amber-50 to-orange-50 dark:border-amber-500/30 dark:from-amber-500/10 dark:to-orange-500/10'
        : 'border border-green-200 bg-gradient-to-r from-green-50 to-emerald-50 dark:border-green-500/30 dark:from-green-500/10 dark:to-emerald-500/10'
    }`}>
      <div className="flex flex-col items-start justify-between gap-4 sm:flex-row sm:items-center">
        <div className="flex items-center gap-3">
          {hasIssues ? (
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-amber-100 dark:bg-amber-500/20">
              <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400" />
            </div>
          ) : (
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-green-100 dark:bg-green-500/20">
              <ShieldCheck className="h-5 w-5 text-green-600 dark:text-green-400" />
            </div>
          )}
          <div>
            {hasIssues ? (
              <>
                <p className="font-medium text-amber-800 dark:text-amber-300">
                  {criticalCount} alert critici attivi
                </p>
                <p className="text-sm text-amber-600 dark:text-amber-400">
                  {healthyServices}/{totalServices} servizi operativi
                </p>
              </>
            ) : (
              <>
                <p className="font-medium text-green-800 dark:text-green-300">
                  Tutti i sistemi operativi
                </p>
                <p className="text-sm text-green-600 dark:text-green-400">
                  {totalServices}/{totalServices} servizi in salute
                </p>
              </>
            )}
          </div>
        </div>

        <button className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${
          hasIssues
            ? 'bg-amber-500 text-white hover:bg-amber-600'
            : 'bg-green-500 text-white hover:bg-green-600'
        }`}>
          Vai agli Alert
        </button>
      </div>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export default function AdminDashboardRedesign() {
  return (
    <div className="min-h-screen bg-stone-50 dark:bg-stone-950">
      <div className="mx-auto max-w-7xl space-y-6 p-4 md:p-6 lg:p-8">
        {/* Header */}
        <DashboardHeader adminName="System Admin" />

        {/* KPI Cards */}
        <KPICardsGrid cards={mockKPIs} />

        {/* Main Content Grid */}
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* Left Column */}
          <div className="space-y-6">
            <SystemHealthMatrix services={mockServices} />
            <ActivityTimeline activities={mockActivity} />
          </div>

          {/* Right Column */}
          <div className="space-y-6">
            <QuickActionsPanel />
            <PendingApprovalsWidget approvals={mockPendingApprovals} />
          </div>
        </div>

        {/* Charts Section */}
        <ChartsSection />

        {/* Alerts Banner */}
        <AlertsBanner />
      </div>
    </div>
  );
}
