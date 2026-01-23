/**
 * AlertsBanner Component - Issue #2791 (Updated for Issue #2792)
 *
 * System-wide alerts banner displaying real-time alert status from metrics.
 * Fully integrated with useDashboardData hook.
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

'use client';

import { motion, AnimatePresence, useReducedMotion } from 'framer-motion';
import { AlertTriangle, ShieldCheck } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import { variants } from '@/lib/animations/variants';
import type { DashboardMetrics } from '@/lib/api';

interface AlertsBannerProps {
  /** Dashboard metrics for deriving alert status */
  metrics: DashboardMetrics | null;
  /** Number of healthy services */
  healthyServices: number;
  /** Total number of services */
  totalServices: number;
  /** Additional CSS classes */
  className?: string;
}

/**
 * AlertsBanner - System status summary banner
 *
 * Displays system health status with dynamic styling:
 * - Has Issues: Amber gradient with pulsating alert indicator
 * - All Healthy: Green gradient with shield check icon
 *
 * Features:
 * - Smooth state transitions with Framer Motion
 * - Decorative blur and corner gradient effects
 * - Accessibility support with reduced motion
 * - Dark mode compatible
 * - Navigation to alerts panel
 *
 * @example
 * ```tsx
 * <AlertsBanner
 *   metrics={dashboardMetrics}
 *   healthyServices={8}
 *   totalServices={10}
 * />
 * ```
 */
export function AlertsBanner({
  metrics,
  healthyServices,
  totalServices,
  className,
}: AlertsBannerProps): JSX.Element | null {
  const router = useRouter();
  const shouldReduceMotion = useReducedMotion();

  // Extract critical count from metrics
  const criticalCount = metrics?.activeAlerts ?? 0;

  // Validate and normalize props
  const safeCriticalCount = Math.max(0, criticalCount);
  const safeHealthyServices = Math.max(0, Math.min(healthyServices, totalServices));
  const safeTotalServices = Math.max(0, totalServices);

  // Compute state - consider both alerts and error rate
  const errorRate = metrics?.errorRate24h ?? 0;
  const hasIssues = safeCriticalCount > 0 || safeHealthyServices < safeTotalServices || errorRate > 0.05;

  // Messages with error rate context
  const primaryMessage =
    safeTotalServices === 0
      ? 'Nessun servizio configurato'
      : hasIssues
        ? safeCriticalCount > 0
          ? `${safeCriticalCount} alert critic${safeCriticalCount === 1 ? 'o' : 'i'} attiv${safeCriticalCount === 1 ? 'o' : 'i'}`
          : errorRate > 0.1
            ? `Tasso di errore elevato (${(errorRate * 100).toFixed(1)}%)`
            : 'Anomalie di sistema rilevate'
        : 'Tutti i sistemi operativi';

  const secondaryMessage =
    safeTotalServices === 0
      ? 'Configura i servizi da monitorare'
      : hasIssues
        ? `${safeHealthyServices}/${safeTotalServices} servizi operativi`
        : `${safeTotalServices}/${safeTotalServices} servizi in salute`;

  // Icon and colors
  const Icon = hasIssues ? AlertTriangle : ShieldCheck;

  // Animation variants (disabled if reduced motion)
  const motionProps = shouldReduceMotion
    ? {}
    : {
        variants: variants.fadeIn,
        initial: 'initial',
        animate: 'animate',
      };

  return (
    <motion.div
      {...motionProps}
      className={cn(
        'group relative overflow-hidden rounded-xl border-2 transition-all duration-300 hover:shadow-lg',
        hasIssues
          ? 'border-amber-200 bg-white dark:border-amber-800 dark:bg-stone-900'
          : 'border-green-200 bg-white dark:border-green-800 dark:bg-stone-900',
        className
      )}
    >
      {/* Blur decoration */}
      {!shouldReduceMotion && (
        <AnimatePresence mode="wait">
          <motion.div
            key={hasIssues ? 'amber' : 'green'}
            initial={{ opacity: 0 }}
            animate={{ opacity: 0.1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.5 }}
            className={cn(
              'absolute -right-12 -top-12 h-32 w-32 rounded-full blur-3xl',
              hasIssues ? 'bg-amber-500' : 'bg-green-500'
            )}
            aria-hidden="true"
          />
        </AnimatePresence>
      )}

      {/* Decorative corner */}
      <div
        className={cn(
          'absolute -right-6 -top-6 h-16 w-16 rounded-full transition-transform duration-300',
          !shouldReduceMotion && 'group-hover:scale-150',
          hasIssues
            ? 'bg-gradient-to-br from-amber-500/10 to-orange-500/10'
            : 'bg-gradient-to-br from-green-500/10 to-emerald-500/10'
        )}
        aria-hidden="true"
      />

      {/* Content */}
      <div className="relative flex items-center gap-4 p-6">
        {/* Icon box with pulsating dot */}
        <div className="relative">
          <motion.div
            layout={!shouldReduceMotion}
            transition={shouldReduceMotion ? undefined : { type: 'spring', bounce: 0.3 }}
            className={cn(
              'flex h-14 w-14 items-center justify-center rounded-xl transition-colors',
              hasIssues
                ? 'bg-amber-100 text-amber-600 dark:bg-amber-500/20 dark:text-amber-400'
                : 'bg-green-100 text-green-600 dark:bg-green-500/20 dark:text-green-400'
            )}
            aria-hidden="true"
          >
            {shouldReduceMotion ? (
              <Icon className="h-7 w-7" />
            ) : (
              <AnimatePresence mode="wait">
                <motion.div
                  key={hasIssues ? 'alert' : 'check'}
                  initial={{ scale: 0.8, opacity: 0 }}
                  animate={{ scale: 1, opacity: 1 }}
                  exit={{ scale: 0.8, opacity: 0 }}
                  transition={{ type: 'spring', bounce: 0.4 }}
                >
                  <Icon className="h-7 w-7" />
                </motion.div>
              </AnimatePresence>
            )}
          </motion.div>

          {/* Pulsating status dot (only for critical alerts) */}
          {hasIssues && (
            <span className="absolute right-0 top-0 flex h-3 w-3" aria-hidden="true">
              {!shouldReduceMotion && (
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-amber-400 opacity-75" />
              )}
              <span className="relative inline-flex h-3 w-3 rounded-full bg-amber-500" />
            </span>
          )}
        </div>

        {/* Text content */}
        <motion.div
          variants={shouldReduceMotion ? undefined : variants.staggerContainer}
          initial={shouldReduceMotion ? undefined : 'initial'}
          animate={shouldReduceMotion ? undefined : 'animate'}
          className="flex-1"
        >
          <motion.p
            variants={shouldReduceMotion ? undefined : variants.fadeIn}
            className="font-semibold text-lg text-stone-900 dark:text-white"
          >
            {primaryMessage}
          </motion.p>
          <motion.p
            variants={shouldReduceMotion ? undefined : variants.fadeIn}
            className="text-sm text-stone-600 dark:text-stone-400"
          >
            {secondaryMessage}
          </motion.p>
        </motion.div>

        {/* Navigation button */}
        <motion.div variants={shouldReduceMotion ? undefined : variants.scaleIn}>
          <Button
            onClick={() => router.push('/admin/alerts')}
            variant={hasIssues ? 'outline' : 'default'}
            className={cn(
              'transition-all',
              hasIssues
                ? 'border-amber-300 hover:bg-amber-50 dark:border-amber-700 dark:hover:bg-amber-900/20'
                : 'bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-700 hover:to-emerald-700'
            )}
            aria-label={`Visualizza pannello alert di sistema${hasIssues ? ` (${safeCriticalCount} critici attivi)` : ''}`}
          >
            Vai agli Alert
          </Button>
        </motion.div>
      </div>
    </motion.div>
  );
}
