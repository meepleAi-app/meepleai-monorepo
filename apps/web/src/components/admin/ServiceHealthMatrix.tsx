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
          'flex flex-col items-center justify-center p-12 text-center border-2 border-dashed border-border/50 dark:border-border/30 rounded-lg bg-muted/50 dark:bg-card',
          className
        )}
        data-testid="service-health-matrix-empty"
      >
        <AlertCircle className="h-12 w-12 text-muted-foreground/50 mb-4" aria-hidden="true" />
        <p className="text-lg font-medium text-foreground mb-1">
          {i18n.labels.noData}
        </p>
        <p className="text-sm text-muted-foreground">
          {locale === 'it'
            ? 'Nessun servizio disponibile al momento.'
            : 'No services available at the moment.'}
        </p>
      </div>
    );
  }

  // Main grid
  // Safe layout class access
   
  const layoutClass = layout in layoutClasses ? layoutClasses[layout] : layoutClasses.auto;
  return (
    <div
      className={cn('grid gap-4', layoutClass, className)}
      data-testid="service-health-matrix"
      role="list"
      aria-label={i18n.grid.title}
    >
      {services.map(service => {
        // responseTimeMs is now directly available from the API
        const lastCheck = service.checkedAt ? new Date(service.checkedAt) : undefined;

        return (
          <div key={service.serviceName} role="listitem">
            <ServiceCard
              serviceName={service.serviceName}
              status={service.state}
              errorMessage={service.errorMessage}
              responseTimeMs={service.responseTimeMs}
              lastCheck={lastCheck}
              locale={locale}
            />
          </div>
        );
      })}
    </div>
  );
}
