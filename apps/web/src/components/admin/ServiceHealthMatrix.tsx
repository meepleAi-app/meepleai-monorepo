/**
 * ServiceHealthMatrix Component - Issue #896
 *
 * Grid display of infrastructure service health statuses.
 * Uses ServiceCard for individual services, responsive grid layout.
 *
 * Features:
 * - Dynamic grid layout (supports any number of services)
 * - Loading state
 * - Empty state
 * - Responsive (1-col mobile, 2-col tablet, 3-col desktop)
 * - i18n support
 */

import { AlertCircle } from 'lucide-react';

import type { ServiceHealthStatus } from '@/lib/api';
import { getInfrastructureI18n, type Locale } from '@/lib/i18n/infrastructure';
import { cn } from '@/lib/utils';

import { ServiceCard } from './ServiceCard';

export interface ServiceHealthMatrixProps {
  /** Array of service health statuses from API */
  services?: ServiceHealthStatus[];
  /** Loading state */
  loading?: boolean;
  /** Current locale */
  locale?: Locale;
  /** Grid layout: auto (responsive) or fixed columns */
  layout?: 'auto' | 'grid-2' | 'grid-3' | 'grid-4';
  /** Additional CSS classes */
  className?: string;
}

const layoutClasses = {
  auto: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4',
  'grid-2': 'grid-cols-1 md:grid-cols-2',
  'grid-3': 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
  'grid-4': 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4',
};

/**
 * Parse TimeSpan string from backend (e.g., "00:00:00.0150000") to milliseconds
 */
function parseTimeSpanToMs(timeSpan: string): number {
  try {
    const parts = timeSpan.split(':');
    if (parts.length !== 3) return 0;

    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    const secondsParts = parts[2].split('.');
    const seconds = parseInt(secondsParts[0], 10);
    const fractionalSeconds = secondsParts[1] ? parseFloat(`0.${secondsParts[1]}`) : 0;

    return (hours * 3600 + minutes * 60 + seconds) * 1000 + fractionalSeconds * 1000;
  } catch {
    return 0;
  }
}

export function ServiceHealthMatrix({
  services = [],
  loading = false,
  locale = 'it',
  layout = 'auto',
  className,
}: ServiceHealthMatrixProps) {
  const i18n = getInfrastructureI18n(locale);

  // Loading skeleton
  if (loading) {
    const skeletonCount = 6; // Default skeleton count
    // Safe layout class access
    // eslint-disable-next-line security/detect-object-injection -- layout is typed union of literal strings
    const layoutClass = layout in layoutClasses ? layoutClasses[layout] : layoutClasses.auto;
    return (
      <div
        className={cn('grid gap-4', layoutClass, className)}
        data-testid="service-health-matrix-loading"
        role="status"
        aria-label={i18n.labels.loading}
      >
        {Array.from({ length: skeletonCount }).map((_, i) => (
          <ServiceCard
            key={`skeleton-${i}`}
            serviceName="loading"
            status="Healthy"
            loading={true}
            locale={locale}
          />
        ))}
      </div>
    );
  }

  // Empty state
  if (services.length === 0) {
    return (
      <div
        className={cn(
          'flex flex-col items-center justify-center p-12 text-center border-2 border-dashed border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-800/50',
          className
        )}
        data-testid="service-health-matrix-empty"
      >
        <AlertCircle className="h-12 w-12 text-gray-400 dark:text-gray-500 mb-4" aria-hidden="true" />
        <p className="text-lg font-medium text-gray-900 dark:text-gray-100 mb-1">
          {i18n.labels.noData}
        </p>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          {locale === 'it'
            ? 'Nessun servizio disponibile al momento.'
            : 'No services available at the moment.'}
        </p>
      </div>
    );
  }

  // Main grid
  // Safe layout class access
  // eslint-disable-next-line security/detect-object-injection -- layout is typed union of literal strings
  const layoutClass = layout in layoutClasses ? layoutClasses[layout] : layoutClasses.auto;
  return (
    <div
      className={cn('grid gap-4', layoutClass, className)}
      data-testid="service-health-matrix"
      role="list"
      aria-label={i18n.grid.title}
    >
      {services.map(service => {
        const responseTimeMs = parseTimeSpanToMs(service.responseTime);
        const lastCheck = service.checkedAt ? new Date(service.checkedAt) : undefined;

        return (
          <div key={service.serviceName} role="listitem">
            <ServiceCard
              serviceName={service.serviceName}
              status={service.state}
              errorMessage={service.errorMessage}
              responseTimeMs={responseTimeMs}
              lastCheck={lastCheck}
              locale={locale}
            />
          </div>
        );
      })}
    </div>
  );
}
