/**
 * ServiceCard Component - Issue #896
 *
 * Admin-specific wrapper around the generic StatusCard component.
 * Provides infrastructure service health display with i18n support.
 *
 * Features:
 * - Health state badge (healthy/degraded/unhealthy)
 * - Response time metric
 * - Last check timestamp
 * - Error message display
 * - i18n support (IT/EN) via infrastructure i18n
 *
 * @module components/admin/ServiceCard
 * @see Issue #2925 - Component Library extraction
 */

import { StatusCard, type StatusState } from '@/components/ui/data-display/status-card';
import type { HealthState } from '@/lib/api';
import { getInfrastructureI18n, type Locale } from '@/lib/i18n/infrastructure';

export interface ServiceCardProps {
  /** Service name (e.g., 'postgres', 'redis') */
  serviceName: string;
  /** Health state from backend */
  status: HealthState;
  /** Optional error message for degraded/unhealthy states */
  errorMessage?: string | null;
  /** Response time in milliseconds */
  responseTimeMs?: number;
  /** Last check timestamp */
  lastCheck?: Date;
  /** Loading state */
  loading?: boolean;
  /** Current locale for i18n */
  locale?: Locale;
  /** Additional CSS classes */
  className?: string;
}

/**
 * Map backend HealthState to generic StatusState
 */
function mapHealthState(status: HealthState): StatusState {
  switch (status) {
    case 'Healthy':
      return 'healthy';
    case 'Degraded':
      return 'degraded';
    case 'Unhealthy':
      return 'unhealthy';
    default:
      return 'unhealthy';
  }
}

/**
 * ServiceCard - Admin infrastructure service health card
 *
 * Wraps the generic StatusCard with admin-specific i18n and service name resolution.
 */
export function ServiceCard({
  serviceName,
  status,
  errorMessage,
  responseTimeMs,
  lastCheck,
  loading = false,
  locale = 'it',
  className,
}: ServiceCardProps) {
  const i18n = getInfrastructureI18n(locale);

  // Resolve service display name from i18n
  // Use Object.hasOwn for safe property access
  const displayName = Object.hasOwn(i18n.services, serviceName)
    ? (i18n.services[serviceName as keyof typeof i18n.services] as string)
    : serviceName;

  // Build labels for StatusCard
  const labels = {
    status: i18n.labels.status,
    responseTime: i18n.labels.responseTime,
    lastCheck: i18n.labels.lastCheck,
    errorMessage: i18n.labels.errorMessage,
    now: locale === 'it' ? 'Ora' : 'Now',
    minAgo: locale === 'it' ? 'min fa' : 'min ago',
    hoursAgo: locale === 'it' ? 'h fa' : 'h ago',
    // Translated status labels
    healthy: i18n.states.Healthy,
    degraded: i18n.states.Degraded,
    unhealthy: i18n.states.Unhealthy,
  };

  return (
    <StatusCard
      name={displayName}
      status={mapHealthState(status)}
      errorMessage={errorMessage}
      latencyMs={responseTimeMs}
      lastCheck={lastCheck}
      loading={loading}
      labels={labels}
      className={className}
    />
  );
}
