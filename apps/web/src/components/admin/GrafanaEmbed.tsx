/* eslint-disable security/detect-object-injection -- Safe dashboard config Record access */
'use client';

import React, { useState, useEffect } from 'react';

import {
  AlertCircle,
  ExternalLink,
  Server,
  DollarSign,
  Activity,
  Brain,
  RefreshCw,
} from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { GRAFANA_DASHBOARDS, buildGrafanaEmbedUrl } from '@/config/grafana-dashboards';

interface GrafanaEmbedProps {
  locale?: 'it' | 'en';
  defaultDashboard?: string;
  autoRefresh?: string; // e.g., '30s', '1m', '5m'
  timeRange?: {
    from: string; // e.g., 'now-1h', 'now-6h'
    to: string; // e.g., 'now'
  };
  className?: string;
}

const iconMap: Record<string, React.ElementType> = {
  Server,
  DollarSign,
  Activity,
  Brain,
};

export function GrafanaEmbed({
  locale = 'en',
  defaultDashboard = 'infrastructure',
  autoRefresh = '30s',
  timeRange = { from: 'now-1h', to: 'now' },
  className,
}: GrafanaEmbedProps) {
  const [selectedDashboard, setSelectedDashboard] = useState(defaultDashboard);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [iframeKey, setIframeKey] = useState(0);

  const currentDashboard = GRAFANA_DASHBOARDS.find(d => d.id === selectedDashboard);

  useEffect(() => {
    // Reset loading state when dashboard changes
    setIsLoading(true);
    setHasError(false);
  }, [selectedDashboard]);

  const handleIframeLoad = () => {
    setIsLoading(false);
    setHasError(false);
  };

  const handleIframeError = () => {
    setIsLoading(false);
    setHasError(true);
  };

  const handleRefresh = () => {
    setIframeKey(prev => prev + 1);
    setIsLoading(true);
    setHasError(false);
  };

  const handleOpenExternal = () => {
    if (currentDashboard) {
      const url = buildGrafanaEmbedUrl(currentDashboard, {
        refresh: autoRefresh,
        from: timeRange.from,
        to: timeRange.to,
        kiosk: false, // Show full Grafana UI in external window
      });
      window.open(url, '_blank', 'noopener,noreferrer');
    }
  };

  const getIcon = (iconName?: string) => {
    if (!iconName) return Server;
    return iconMap[iconName] || Server;
  };

  if (!currentDashboard) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          {locale === 'it'
            ? `Dashboard non trovata: ${selectedDashboard}`
            : `Dashboard not found: ${selectedDashboard}`}
        </AlertDescription>
      </Alert>
    );
  }

  const embedUrl = buildGrafanaEmbedUrl(currentDashboard, {
    refresh: autoRefresh,
    from: timeRange.from,
    to: timeRange.to,
    kiosk: true,
  });

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              {locale === 'it' ? 'Dashboard Grafana' : 'Grafana Dashboards'}
            </CardTitle>
            <CardDescription>
              {locale === 'it'
                ? 'Visualizza metriche in tempo reale da Grafana'
                : 'View real-time metrics from Grafana'}
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="icon" onClick={handleRefresh} title="Refresh">
              <RefreshCw className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              onClick={handleOpenExternal}
              title={locale === 'it' ? 'Apri in Grafana' : 'Open in Grafana'}
            >
              <ExternalLink className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <Tabs value={selectedDashboard} onValueChange={setSelectedDashboard}>
          <TabsList className="grid w-full grid-cols-2 lg:grid-cols-4 mb-4">
            {GRAFANA_DASHBOARDS.map(dashboard => {
              const Icon = getIcon(dashboard.icon);
              return (
                <TabsTrigger key={dashboard.id} value={dashboard.id} className="gap-2">
                  <Icon className="h-4 w-4" />
                  <span className="hidden sm:inline">{dashboard.name[locale]}</span>
                </TabsTrigger>
              );
            })}
          </TabsList>

          {GRAFANA_DASHBOARDS.map(dashboard => (
            <TabsContent key={dashboard.id} value={dashboard.id} className="space-y-4">
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{dashboard.description[locale]}</AlertDescription>
              </Alert>

              <div className="relative aspect-video bg-muted dark:bg-card rounded-lg overflow-hidden border border-border/50 dark:border-border/30">
                {isLoading && (
                  <div className="absolute inset-0 flex items-center justify-center bg-card z-10">
                    <div className="text-center space-y-4">
                      <Skeleton className="h-64 w-full" />
                      <p className="text-sm text-muted-foreground">
                        {locale === 'it' ? 'Caricamento dashboard...' : 'Loading dashboard...'}
                      </p>
                    </div>
                  </div>
                )}

                {hasError ? (
                  <div className="absolute inset-0 flex items-center justify-center bg-muted/50 dark:bg-card">
                    <div className="text-center space-y-4 p-6">
                      <AlertCircle className="h-16 w-16 text-red-500 mx-auto" />
                      <div>
                        <p className="text-lg font-medium text-foreground mb-2">
                          {locale === 'it'
                            ? 'Errore caricamento dashboard'
                            : 'Error loading dashboard'}
                        </p>
                        <p className="text-sm text-muted-foreground mb-4">
                          {locale === 'it'
                            ? 'Verifica che Grafana sia in esecuzione'
                            : 'Check that Grafana is running'}
                        </p>
                      </div>
                      <div className="flex gap-2 justify-center">
                        <Button variant="outline" onClick={handleRefresh}>
                          {locale === 'it' ? 'Riprova' : 'Retry'}
                        </Button>
                        <Button variant="default" onClick={handleOpenExternal}>
                          {locale === 'it' ? 'Apri in Grafana' : 'Open in Grafana'}
                        </Button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <iframe
                    key={iframeKey}
                    src={embedUrl}
                    className="w-full h-full min-h-[600px]"
                    title={dashboard.name[locale]}
                    onLoad={handleIframeLoad}
                    onError={handleIframeError}
                    allow="fullscreen"
                    sandbox="allow-same-origin allow-scripts allow-forms allow-downloads"
                  />
                )}
              </div>

              <div className="flex justify-between items-center text-xs text-gray-500 dark:text-gray-400">
                <span>
                  {locale === 'it' ? 'Auto-aggiornamento: ' : 'Auto-refresh: '}
                  {autoRefresh}
                </span>
                <span>
                  {locale === 'it' ? 'Intervallo: ' : 'Time range: '}
                  {timeRange.from} → {timeRange.to}
                </span>
              </div>
            </TabsContent>
          ))}
        </Tabs>
      </CardContent>
    </Card>
  );
}
