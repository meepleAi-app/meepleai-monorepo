/**
 * Service Status Card Component
 *
 * Issue #2516: Displays individual service health status with:
 * - Color-coded borders (green/yellow/red)
 * - Service name + description
 * - Critical badge if applicable
 * - Last check timestamp
 * - "Test Now" action button
 *
 * Card States:
 * - Healthy: ✅ green border, CheckCircle icon, green-50 background
 * - Degraded: ⚠️ yellow border, AlertTriangle icon, yellow-50 background
 * - Unhealthy: ❌ red border, XCircle icon, red-50 background
 */

import { CheckCircleIcon, AlertTriangleIcon, XCircleIcon, PlayIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import type { ServiceHealthStatus } from '@/lib/api';

interface ServiceStatusCardProps {
  service: ServiceHealthStatus;
  isCritical: boolean;
  onTestNow: () => void;
  locale: string;
}

const stateConfig = {
  Healthy: {
    icon: CheckCircleIcon,
    borderColor: 'border-green-500',
    bgColor: 'bg-green-50 dark:bg-green-950',
    iconColor: 'text-green-600 dark:text-green-400',
    label: { it: 'Operativo', en: 'Healthy' },
  },
  Degraded: {
    icon: AlertTriangleIcon,
    borderColor: 'border-yellow-500',
    bgColor: 'bg-yellow-50 dark:bg-yellow-950',
    iconColor: 'text-yellow-600 dark:text-yellow-400',
    label: { it: 'Degradato', en: 'Degraded' },
  },
  Unhealthy: {
    icon: XCircleIcon,
    borderColor: 'border-red-500',
    bgColor: 'bg-red-50 dark:bg-red-950',
    iconColor: 'text-red-600 dark:text-red-400',
    label: { it: 'Non Disponibile', en: 'Unhealthy' },
  },
};

// Service descriptions (hardcoded - could come from API metadata)
const serviceDescriptions: Record<string, { it: string; en: string }> = {
  postgres: {
    it: 'Database principale per dati applicativi',
    en: 'Primary database for application data',
  },
  qdrant: {
    it: 'Vector database per ricerca semantica',
    en: 'Vector database for semantic search',
  },
  redis: {
    it: 'Cache distribuita e gestione sessioni',
    en: 'Distributed cache and session management',
  },
  n8n: {
    it: 'Motore di automazione workflow',
    en: 'Workflow automation engine',
  },
  hyperdx: {
    it: 'Piattaforma di osservabilità e logging',
    en: 'Observability and logging platform',
  },
};

export function ServiceStatusCard({
  service,
  isCritical,
  onTestNow,
  locale,
}: ServiceStatusCardProps) {
  const config = stateConfig[service.state];
  const Icon = config.icon;
  const description =
    serviceDescriptions[service.serviceName.toLowerCase()]?.[locale === 'it' ? 'it' : 'en'] ||
    (locale === 'it' ? 'Servizio di sistema' : 'System service');

  // Format timestamp
  const formatTimestamp = (isoString: string) => {
    try {
      return new Date(isoString).toLocaleTimeString(locale, {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      });
    } catch {
      return isoString;
    }
  };

  return (
    <Card
      data-testid="service-card"
      className={`transition-all hover:-translate-y-0.5 hover:shadow-lg ${config.borderColor} ${config.bgColor} border-2`}
    >
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-2">
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <CardTitle className="text-lg font-semibold capitalize">
              {service.serviceName}
            </CardTitle>
            {isCritical && (
              <Badge variant="destructive" className="text-xs">
                {locale === 'it' ? 'Critico' : 'Critical'}
              </Badge>
            )}
          </div>
          <CardDescription className="mt-1 text-xs">{description}</CardDescription>
        </div>
        <Icon className={`h-6 w-6 ${config.iconColor}`} />
      </CardHeader>

      <CardContent className="space-y-3">
        {/* Status */}
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">
            {locale === 'it' ? 'Stato:' : 'Status:'}
          </span>
          <Badge variant="outline" className={config.iconColor}>
            {config.label[locale === 'it' ? 'it' : 'en']}
          </Badge>
        </div>

        {/* Last Check */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            {locale === 'it' ? 'Ultimo check:' : 'Last check:'}
          </span>
          <span className="font-mono text-xs">{formatTimestamp(service.checkedAt)}</span>
        </div>

        {/* Error Message (if unhealthy) */}
        {service.errorMessage && (
          <div className="rounded-md bg-red-100 p-2 dark:bg-red-950">
            <p className="text-xs text-red-800 dark:text-red-200">{service.errorMessage}</p>
          </div>
        )}

        {/* Test Now Button */}
        <Button variant="outline" size="sm" className="w-full" onClick={onTestNow}>
          <PlayIcon className="mr-2 h-3 w-3" />
          {locale === 'it' ? 'Testa Ora' : 'Test Now'}
        </Button>
      </CardContent>
    </Card>
  );
}
