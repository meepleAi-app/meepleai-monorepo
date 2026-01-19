/**
 * Service Status Dashboard Client Component
 *
 * Issue #2516: Real-time service health monitoring dashboard
 *
 * Features:
 * - Real-time service status (30s polling)
 * - 3-column responsive grid
 * - Filters: All/Critical/Unhealthy
 * - Export: JSON/CSV
 * - Toast notifications on state changes
 * - Circuit breaker (5 failures = pause)
 *
 * Architecture:
 * - Client Component with useState/useEffect
 * - Direct API calls (no React Query - different from existing pattern)
 * - AdminLayout integration
 * - i18n support (Italian + English)
 */

'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

import {
  RefreshCwIcon,
  DownloadIcon,
  FilterIcon,
  SearchIcon,
  ServerIcon,
  ClockIcon,
} from 'lucide-react';
import { toast } from 'sonner';

import { AdminLayout } from '@/components/admin/AdminLayout';
import { ServiceStatusCard } from '@/components/admin/ServiceStatusCard';
import { OverallStatusBadge } from '@/components/admin/OverallStatusBadge';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useUserLocale } from '@/hooks/useUserLocale';
import { api } from '@/lib/api';
import type { InfrastructureDetails, ServiceHealthStatus, HealthState } from '@/lib/api';

type FilterMode = 'all' | 'critical' | 'unhealthy';

interface PreviousServiceState {
  [serviceName: string]: HealthState;
}

export function ServicesClient() {
  const locale = useUserLocale();

  // State
  const [data, setData] = useState<InfrastructureDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filterMode, setFilterMode] = useState<FilterMode>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  // Circuit breaker
  const [failureCount, setFailureCount] = useState(0);
  const MAX_FAILURES = 5;
  const circuitOpen = failureCount >= MAX_FAILURES;

  // Track previous service states for change detection
  const previousStates = useRef<PreviousServiceState>({});

  // Critical services (hardcoded - could come from API metadata)
  const criticalServices = ['postgres', 'qdrant', 'redis'];

  // Fetch service health data
  const fetchData = useCallback(async () => {
    if (circuitOpen) {
      console.warn('[ServicesClient] Circuit breaker open, skipping fetch');
      return;
    }

    try {
      setError(null);
      const result = await api.admin.getInfrastructureDetails();

      if (result) {
        // Detect state changes for notifications
        const currentStates: PreviousServiceState = {};
        result.services.forEach(service => {
          currentStates[service.serviceName] = service.state;

          const prevState = previousStates.current[service.serviceName];
          if (prevState && prevState !== service.state) {
            // State changed - show toast notification
            const isCritical = criticalServices.includes(service.serviceName);
            const isNowUnhealthy = service.state !== 'Healthy';

            if (isNowUnhealthy && isCritical) {
              toast.error(
                locale === 'it'
                  ? `${service.serviceName} è ora ${service.state}`
                  : `${service.serviceName} is now ${service.state}`,
                {
                  description:
                    locale === 'it'
                      ? 'Servizio critico degradato o non disponibile'
                      : 'Critical service degraded or unavailable',
                }
              );
            } else if (!isNowUnhealthy && prevState !== 'Healthy') {
              toast.success(
                locale === 'it'
                  ? `${service.serviceName} è ora Healthy`
                  : `${service.serviceName} is now Healthy`,
                {
                  description:
                    locale === 'it' ? 'Servizio ripristinato' : 'Service restored',
                }
              );
            }
          }
        });

        previousStates.current = currentStates;
        setData(result);
        setLastUpdated(new Date());
        setFailureCount(0); // Reset on success
      }
    } catch (err) {
      console.error('[ServicesClient] Fetch error:', err);
      setFailureCount(prev => {
        const newCount = prev + 1;

        if (newCount >= MAX_FAILURES) {
          setError(
            locale === 'it'
              ? 'Troppe richieste fallite. Aggiornamento automatico sospeso.'
              : 'Too many failed requests. Auto-refresh paused.'
          );
        } else {
          setError(
            locale === 'it'
              ? 'Errore caricamento stato servizi'
              : 'Error loading service status'
          );
        }

        return newCount;
      });
    } finally {
      setLoading(false);
    }
  }, [circuitOpen, locale, criticalServices]);

  // Initial fetch
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Auto-refresh every 30 seconds
  useEffect(() => {
    if (circuitOpen) return;

    const interval = setInterval(() => {
      fetchData();
    }, 30000); // 30 seconds

    return () => clearInterval(interval);
  }, [fetchData, circuitOpen]);

  // Manual refresh
  const handleRefresh = async () => {
    setLoading(true);
    await fetchData();
  };

  // Reset circuit breaker
  const handleResetCircuit = () => {
    setFailureCount(0);
    setError(null);
    handleRefresh();
  };

  // Test Now - single service refresh
  const handleTestNow = async (serviceName: string) => {
    toast.info(
      locale === 'it'
        ? `Test ${serviceName} in corso...`
        : `Testing ${serviceName}...`
    );

    // In a real implementation, this would call a specific test endpoint
    // For now, we just trigger a full refresh
    await handleRefresh();
  };

  // Filter services
  const filteredServices = data?.services
    .filter(service => {
      // Filter by mode
      if (filterMode === 'critical' && !criticalServices.includes(service.serviceName)) {
        return false;
      }
      if (filterMode === 'unhealthy' && service.state === 'Healthy') {
        return false;
      }

      // Filter by search query
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        return service.serviceName.toLowerCase().includes(query);
      }

      return true;
    })
    .sort((a, b) => {
      // Sort: Unhealthy → Degraded → Healthy
      const stateOrder: Record<HealthState, number> = {
        Unhealthy: 0,
        Degraded: 1,
        Healthy: 2,
      };
      return stateOrder[a.state] - stateOrder[b.state];
    });

  // Export functionality
  const handleExport = (format: 'csv' | 'json') => {
    if (!data) return;

    const filename = `service-status-${new Date().toISOString()}.${format}`;

    if (format === 'json') {
      const exportData = {
        timestamp: new Date().toISOString(),
        overall: data.overall,
        services: data.services.map(s => ({
          name: s.serviceName,
          state: s.state,
          critical: criticalServices.includes(s.serviceName),
          checkedAt: s.checkedAt,
          errorMessage: s.errorMessage,
        })),
      };

      const blob = new Blob([JSON.stringify(exportData, null, 2)], {
        type: 'application/json',
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      a.click();
      URL.revokeObjectURL(url);
    } else {
      // CSV format
      const headers = ['Service,Status,Critical,Last Check,Error'];
      const rows = data.services.map(s => {
        const isCritical = criticalServices.includes(s.serviceName);
        return `"${s.serviceName}","${s.state}","${isCritical}","${s.checkedAt}","${s.errorMessage || ''}"`;
      });
      const csv = [headers, ...rows].join('\n');
      const blob = new Blob([csv], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      a.click();
      URL.revokeObjectURL(url);
    }

    toast.success(
      locale === 'it' ? 'Esportazione completata' : 'Export completed',
      {
        description:
          locale === 'it'
            ? `File ${filename} scaricato`
            : `File ${filename} downloaded`,
      }
    );
  };

  return (
    <AdminLayout>
      {/* Page Header */}
      <div className="mb-6">
        <h1 className="text-3xl font-bold tracking-tight">
          {locale === 'it' ? 'Stato Servizi' : 'Service Status'}
        </h1>
        <p className="text-muted-foreground">
          {locale === 'it'
            ? 'Monitoraggio in tempo reale dello stato di salute dei servizi'
            : 'Real-time service health monitoring'}
        </p>
      </div>

      {/* Header with overall status and controls */}
      <div className="mb-6 flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-center gap-4">
          <OverallStatusBadge
            overallState={data?.overall?.state || 'Unhealthy'}
            healthyCount={data?.overall?.healthyServices || 0}
            degradedCount={data?.overall?.degradedServices || 0}
            unhealthyCount={data?.overall?.unhealthyServices || 0}
            totalCount={data?.overall?.totalServices || 0}
          />

          {lastUpdated && (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <ClockIcon className="h-4 w-4" />
              <span>
                {locale === 'it' ? 'Ultimo aggiornamento:' : 'Last updated:'}{' '}
                {lastUpdated.toLocaleTimeString(locale)}
              </span>
            </div>
          )}
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            disabled={loading || circuitOpen}
          >
            <RefreshCwIcon
              className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`}
            />
            {locale === 'it' ? 'Aggiorna' : 'Refresh All'}
          </Button>

          <Select
            value={filterMode}
            onValueChange={value => setFilterMode(value as FilterMode)}
          >
            <SelectTrigger className="w-[160px]">
              <FilterIcon className="mr-2 h-4 w-4" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">
                {locale === 'it' ? 'Tutti' : 'All'}
              </SelectItem>
              <SelectItem value="critical">
                {locale === 'it' ? 'Critici' : 'Critical'}
              </SelectItem>
              <SelectItem value="unhealthy">
                {locale === 'it' ? 'Non Healthy' : 'Unhealthy'}
              </SelectItem>
            </SelectContent>
          </Select>

          <Select defaultValue="json" onValueChange={val => handleExport(val as 'json' | 'csv')}>
            <SelectTrigger className="w-[140px]">
              <DownloadIcon className="mr-2 h-4 w-4" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="json">Export JSON</SelectItem>
              <SelectItem value="csv">Export CSV</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Search input */}
      <div className="mb-4">
        <div className="relative">
          <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="text"
            placeholder={
              locale === 'it'
                ? 'Cerca servizio...'
                : 'Search service...'
            }
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="pl-10"
          />
        </div>
      </div>

      {/* Circuit breaker error */}
      {circuitOpen && (
        <Alert variant="destructive" className="mb-4">
          <AlertDescription>
            {error}
            <Button
              variant="link"
              size="sm"
              onClick={handleResetCircuit}
              className="ml-2"
            >
              {locale === 'it' ? 'Riprova' : 'Retry'}
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* Loading state */}
      {loading && !data && (
        <div className="flex items-center justify-center py-12">
          <RefreshCwIcon className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {/* Service grid */}
      {data && filteredServices && filteredServices.length > 0 && (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {filteredServices.map(service => (
            <ServiceStatusCard
              key={service.serviceName}
              service={service}
              isCritical={criticalServices.includes(service.serviceName)}
              onTestNow={() => handleTestNow(service.serviceName)}
              locale={locale}
            />
          ))}
        </div>
      )}

      {/* Empty state */}
      {data && filteredServices && filteredServices.length === 0 && (
        <Card>
          <CardContent className="py-12 text-center">
            <ServerIcon className="mx-auto h-12 w-12 text-muted-foreground" />
            <p className="mt-4 text-sm text-muted-foreground">
              {locale === 'it'
                ? 'Nessun servizio trovato con i filtri selezionati'
                : 'No services found with selected filters'}
            </p>
          </CardContent>
        </Card>
      )}
    </AdminLayout>
  );
}
