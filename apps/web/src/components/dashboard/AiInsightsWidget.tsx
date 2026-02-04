/**
 * AiInsightsWidget - RAG-powered AI Suggestions Dashboard Widget
 * Issue #3316 - Implement AiInsightsWidget with RAG-powered suggestions
 *
 * Features:
 * - 5 types of insights: backlog, rules_reminder, recommendation, streak, achievement
 * - Yellow/amber card highlight
 * - Click actions for each insight
 * - Graceful degradation if RAG unavailable
 * - Loading skeleton state
 * - Max 5 insights with priority ordering
 *
 * @example
 * ```tsx
 * <AiInsightsWidget />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Lightbulb,
  Target,
  BookOpen,
  Sparkles,
  Flame,
  Trophy,
  ChevronRight,
  AlertCircle,
  RefreshCw,
} from 'lucide-react';
import Link from 'next/link';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type InsightType = 'backlog' | 'rules_reminder' | 'recommendation' | 'streak' | 'achievement';

export interface AiInsight {
  id: string;
  type: InsightType;
  icon: string;
  title: string;
  description: string;
  actionUrl: string;
  actionLabel: string;
  priority: number;
  metadata?: Record<string, unknown>;
}

export interface AiInsightsWidgetProps {
  /** AI insights data */
  insights?: AiInsight[];
  /** Loading state */
  isLoading?: boolean;
  /** Error state */
  error?: Error | null;
  /** AI service unavailable */
  isAiUnavailable?: boolean;
  /** Refresh callback */
  onRefresh?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const INSIGHT_CONFIG: Record<InsightType, { icon: typeof Target; color: string; bgColor: string }> = {
  backlog: {
    icon: Target,
    color: 'text-amber-600 dark:text-amber-400',
    bgColor: 'bg-amber-500/10',
  },
  rules_reminder: {
    icon: BookOpen,
    color: 'text-blue-600 dark:text-blue-400',
    bgColor: 'bg-blue-500/10',
  },
  recommendation: {
    icon: Sparkles,
    color: 'text-purple-600 dark:text-purple-400',
    bgColor: 'bg-purple-500/10',
  },
  streak: {
    icon: Flame,
    color: 'text-orange-600 dark:text-orange-400',
    bgColor: 'bg-orange-500/10',
  },
  achievement: {
    icon: Trophy,
    color: 'text-emerald-600 dark:text-emerald-400',
    bgColor: 'bg-emerald-500/10',
  },
};

const MAX_INSIGHTS = 5;

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_INSIGHTS: AiInsight[] = [
  {
    id: 'insight-1',
    type: 'backlog',
    icon: '🎯',
    title: '5 giochi non giocati da 30+ giorni',
    description: 'Wingspan, Azul e altri aspettano di essere giocati',
    actionUrl: '/library?filter=unplayed',
    actionLabel: 'Scopri',
    priority: 1,
  },
  {
    id: 'insight-2',
    type: 'rules_reminder',
    icon: '📖',
    title: 'Regole di "Wingspan" salvate',
    description: 'Hai salvato le regole 3 giorni fa',
    actionUrl: '/chat/wingspan-rules',
    actionLabel: 'Rivedi',
    priority: 2,
  },
  {
    id: 'insight-3',
    type: 'recommendation',
    icon: '🆕',
    title: '3 giochi simili a "Catan"',
    description: 'Scopri nuovi giochi basati sui tuoi gusti',
    actionUrl: '/games/recommendations?based-on=catan',
    actionLabel: 'Esplora',
    priority: 3,
  },
  {
    id: 'insight-4',
    type: 'streak',
    icon: '🔥',
    title: 'Streak: 7 giorni - Mantienilo!',
    description: 'Gioca oggi per mantenere la tua serie',
    actionUrl: '/stats/streak',
    actionLabel: 'Stats',
    priority: 4,
  },
  {
    id: 'insight-5',
    type: 'achievement',
    icon: '📊',
    title: 'Achievement "Collezionista" al 85%',
    description: 'Ti mancano solo 15 giochi!',
    actionUrl: '/achievements/collector',
    actionLabel: 'Vedi',
    priority: 5,
  },
];

// ============================================================================
// Skeleton Component
// ============================================================================

function AiInsightsWidgetSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-amber-500/30 bg-gradient-to-br from-amber-50/50 to-yellow-50/50 dark:from-amber-950/20 dark:to-yellow-950/20 p-4',
        className
      )}
      data-testid="ai-insights-widget-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-5 w-48" />
        </div>
        <Skeleton className="h-4 w-16" />
      </div>

      {/* Insights Skeleton */}
      <div className="space-y-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <div
            key={i}
            className="flex items-center justify-between p-3 rounded-xl border border-border/40 bg-white/50 dark:bg-card/50"
          >
            <div className="flex items-center gap-3 flex-1">
              <Skeleton className="h-5 w-5 rounded" />
              <div className="flex-1 space-y-1">
                <Skeleton className="h-4 w-48" />
                <Skeleton className="h-3 w-32" />
              </div>
            </div>
            <Skeleton className="h-6 w-16 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Insight Item Component
// ============================================================================

interface InsightItemProps {
  insight: AiInsight;
  index: number;
}

function InsightItem({ insight, index }: InsightItemProps) {
  const config = INSIGHT_CONFIG[insight.type];
  const Icon = config.icon;

  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.08 }}
      className="flex items-center justify-between p-3 rounded-xl border border-amber-200/50 dark:border-amber-800/30 bg-white/60 dark:bg-card/60 hover:bg-white/80 dark:hover:bg-card/80 transition-colors group"
      data-testid={`insight-item-${insight.id}`}
    >
      <div className="flex items-center gap-3 flex-1 min-w-0">
        {/* Icon */}
        <div
          className={cn(
            'h-8 w-8 rounded-lg flex items-center justify-center shrink-0',
            config.bgColor
          )}
          data-testid={`insight-icon-${insight.id}`}
        >
          <Icon className={cn('h-4 w-4', config.color)} />
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <p
            className="text-sm font-medium truncate"
            data-testid={`insight-title-${insight.id}`}
          >
            {insight.title}
          </p>
          <p
            className="text-xs text-muted-foreground truncate"
            data-testid={`insight-description-${insight.id}`}
          >
            {insight.description}
          </p>
        </div>
      </div>

      {/* Action Button */}
      <Link href={insight.actionUrl} className="shrink-0 ml-2">
        <Button
          size="sm"
          variant="ghost"
          className="h-7 px-3 text-xs rounded-full hover:bg-amber-100 dark:hover:bg-amber-900/30 group-hover:bg-amber-100 dark:group-hover:bg-amber-900/30"
          data-testid={`insight-action-${insight.id}`}
        >
          {insight.actionLabel}
          <ChevronRight className="h-3 w-3 ml-1" />
        </Button>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="ai-insights-empty"
    >
      <div className="h-12 w-12 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center mb-3">
        <Lightbulb className="h-6 w-6 text-amber-500" />
      </div>
      <p className="text-sm text-muted-foreground mb-1">
        Nessun suggerimento disponibile
      </p>
      <p className="text-xs text-muted-foreground">
        Continua a giocare per ricevere insight personalizzati
      </p>
    </div>
  );
}

// ============================================================================
// AI Unavailable State Component
// ============================================================================

function AiUnavailableState({ onRefresh }: { onRefresh?: () => void }) {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="ai-insights-unavailable"
    >
      <div className="h-12 w-12 rounded-full bg-muted/50 flex items-center justify-center mb-3">
        <Sparkles className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <p className="text-sm text-muted-foreground mb-1">
        Servizio AI temporaneamente non disponibile
      </p>
      <p className="text-xs text-muted-foreground mb-3">
        I suggerimenti personalizzati saranno disponibili a breve
      </p>
      {onRefresh && (
        <Button
          size="sm"
          variant="outline"
          onClick={onRefresh}
          className="rounded-full"
          data-testid="ai-insights-refresh"
        >
          <RefreshCw className="h-3 w-3 mr-1" />
          Riprova
        </Button>
      )}
    </div>
  );
}

// ============================================================================
// Error State Component
// ============================================================================

function ErrorState({ onRefresh }: { onRefresh?: () => void }) {
  return (
    <Alert variant="destructive" className="py-3" data-testid="ai-insights-error">
      <AlertCircle className="h-4 w-4" />
      <AlertDescription className="text-xs flex items-center justify-between">
        <span>Impossibile caricare i suggerimenti</span>
        {onRefresh && (
          <Button
            size="sm"
            variant="ghost"
            onClick={onRefresh}
            className="h-6 px-2 text-xs"
            data-testid="ai-insights-error-refresh"
          >
            <RefreshCw className="h-3 w-3 mr-1" />
            Riprova
          </Button>
        )}
      </AlertDescription>
    </Alert>
  );
}

// ============================================================================
// AiInsightsWidget Component
// ============================================================================

export function AiInsightsWidget({
  insights = MOCK_INSIGHTS,
  isLoading = false,
  error = null,
  isAiUnavailable = false,
  onRefresh,
  className,
}: AiInsightsWidgetProps) {
  // Sort by priority and limit to MAX_INSIGHTS
  const sortedInsights = useMemo(() => {
    return [...insights]
      .sort((a, b) => a.priority - b.priority)
      .slice(0, MAX_INSIGHTS);
  }, [insights]);

  // Loading state
  if (isLoading) {
    return <AiInsightsWidgetSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-amber-500/30 bg-gradient-to-br from-amber-50/50 to-yellow-50/50 dark:from-amber-950/20 dark:to-yellow-950/20 p-4',
        'shadow-[0_4px_20px_rgba(245,158,11,0.1)]',
        className
      )}
      data-testid="ai-insights-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-amber-500/20 flex items-center justify-center">
            <Lightbulb className="h-4 w-4 text-amber-600 dark:text-amber-400" />
          </div>
          <div>
            <h3
              className="font-semibold text-sm"
              data-testid="ai-insights-title"
            >
              AI Insights & Suggerimenti
            </h3>
            <p className="text-xs text-muted-foreground">
              Powered by RAG
            </p>
          </div>
        </div>

        {onRefresh && !error && !isAiUnavailable && sortedInsights.length > 0 && (
          <Button
            size="sm"
            variant="ghost"
            onClick={onRefresh}
            className="h-7 w-7 p-0 rounded-full hover:bg-amber-100 dark:hover:bg-amber-900/30"
            data-testid="ai-insights-header-refresh"
          >
            <RefreshCw className="h-3.5 w-3.5" />
          </Button>
        )}
      </div>

      {/* Content */}
      {error ? (
        <ErrorState onRefresh={onRefresh} />
      ) : isAiUnavailable ? (
        <AiUnavailableState onRefresh={onRefresh} />
      ) : sortedInsights.length === 0 ? (
        <EmptyState />
      ) : (
        <div className="space-y-2" data-testid="ai-insights-list">
          {sortedInsights.map((insight, index) => (
            <InsightItem key={insight.id} insight={insight} index={index} />
          ))}
        </div>
      )}
    </section>
  );
}

export default AiInsightsWidget;
