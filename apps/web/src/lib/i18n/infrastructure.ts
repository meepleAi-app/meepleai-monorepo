/**
 * Infrastructure Monitoring i18n strings (Issue #896)
 *
 * Multi-language support for ServiceHealthMatrix and ServiceCard components.
 * Follows project principle: no hardcoded values.
 */

export const infrastructurei18n = {
  it: {
    // Service names
    services: {
      postgres: 'PostgreSQL',
      redis: 'Redis',
      pgvector: 'pgvector',
      qdrant: 'pgvector', // legacy key — maps to pgvector after migration
      'qdrant-collection': 'Vector Store', // legacy key
      n8n: 'n8n',
      prometheus: 'Prometheus',
      grafana: 'Grafana',
      api: 'API',
    },
    // Health states
    states: {
      Healthy: 'Sano',
      Degraded: 'Degradato',
      Unhealthy: 'Non funzionante',
    },
    // Component labels
    labels: {
      status: 'Stato',
      responseTime: 'Tempo di risposta',
      lastCheck: 'Ultimo controllo',
      errorMessage: 'Messaggio di errore',
      unknown: 'Sconosciuto',
      loading: 'Caricamento...',
      noData: 'Nessun dato disponibile',
      refresh: 'Aggiorna',
      viewDetails: 'Visualizza dettagli',
    },
    // Grid labels
    grid: {
      title: 'Stato Servizi Infrastruttura',
      overallHealth: 'Salute Complessiva',
      healthyServices: 'Servizi Sani',
      degradedServices: 'Servizi Degradati',
      unhealthyServices: 'Servizi Non Funzionanti',
      totalServices: 'Totale Servizi',
    },
    // Time units
    time: {
      ms: 'ms',
      seconds: 's',
      minutes: 'min',
      hours: 'h',
    },
    // Page-specific labels (Issue #899)
    page: {
      title: 'Monitoraggio Infrastruttura',
      description: 'Stato in tempo reale dei servizi e metriche operative',
      healthStatus: 'Stato di Salute',
      metricsOverview: 'Panoramica Metriche',
      serviceDetails: 'Dettagli Servizi',
      prometheusMetrics: 'Metriche Prometheus',
      grafanaDashboards: 'Dashboard Grafana',
      exportData: 'Esporta Dati',
      filterServices: 'Filtra Servizi',
      sortBy: 'Ordina per',
      showAll: 'Mostra Tutti',
      showHealthy: 'Solo Sani',
      showUnhealthy: 'Solo Problematici',
      lastUpdated: 'Ultimo aggiornamento',
      autoRefresh: 'Aggiornamento automatico',
      refreshInterval: 'Intervallo aggiornamento',
      exportCsv: 'Esporta CSV',
      exportJson: 'Esporta JSON',
    },
    metrics: {
      apiRequests: 'Richieste API (24h)',
      avgLatency: 'Latenza Media',
      errorRate: 'Tasso Errori',
      llmCost: 'Costo LLM (24h)',
      cpuUsage: 'Utilizzo CPU',
      memoryUsage: 'Utilizzo Memoria',
      diskUsage: 'Utilizzo Disco',
      networkTraffic: 'Traffico Rete',
    },
    charts: {
      last1h: 'Ultima 1h',
      last6h: 'Ultime 6h',
      last24h: 'Ultime 24h',
      last7d: 'Ultimi 7 giorni',
      cpuUsageTitle: 'Utilizzo CPU (%)',
      memoryUsageTitle: 'Utilizzo Memoria (MB)',
      apiRequestsTitle: 'Richieste API (req/s)',
      networkTitle: 'Traffico Rete (MB/s)',
    },
  },
  en: {
    // Service names
    services: {
      postgres: 'PostgreSQL',
      redis: 'Redis',
      pgvector: 'pgvector',
      qdrant: 'pgvector', // legacy key — maps to pgvector after migration
      'qdrant-collection': 'Vector Store', // legacy key
      n8n: 'n8n',
      prometheus: 'Prometheus',
      grafana: 'Grafana',
      api: 'API',
    },
    // Health states
    states: {
      Healthy: 'Healthy',
      Degraded: 'Degraded',
      Unhealthy: 'Unhealthy',
    },
    // Component labels
    labels: {
      status: 'Status',
      responseTime: 'Response Time',
      lastCheck: 'Last Check',
      errorMessage: 'Error Message',
      unknown: 'Unknown',
      loading: 'Loading...',
      noData: 'No data available',
      refresh: 'Refresh',
      viewDetails: 'View Details',
    },
    // Grid labels
    grid: {
      title: 'Infrastructure Services Status',
      overallHealth: 'Overall Health',
      healthyServices: 'Healthy Services',
      degradedServices: 'Degraded Services',
      unhealthyServices: 'Unhealthy Services',
      totalServices: 'Total Services',
    },
    // Time units
    time: {
      ms: 'ms',
      seconds: 's',
      minutes: 'min',
      hours: 'h',
    },
    // Page-specific labels (Issue #899)
    page: {
      title: 'Infrastructure Monitoring',
      description: 'Real-time service status and operational metrics',
      healthStatus: 'Health Status',
      metricsOverview: 'Metrics Overview',
      serviceDetails: 'Service Details',
      prometheusMetrics: 'Prometheus Metrics',
      grafanaDashboards: 'Grafana Dashboards',
      exportData: 'Export Data',
      filterServices: 'Filter Services',
      sortBy: 'Sort by',
      showAll: 'Show All',
      showHealthy: 'Healthy Only',
      showUnhealthy: 'Issues Only',
      lastUpdated: 'Last updated',
      autoRefresh: 'Auto-refresh',
      refreshInterval: 'Refresh interval',
      exportCsv: 'Export CSV',
      exportJson: 'Export JSON',
    },
    metrics: {
      apiRequests: 'API Requests (24h)',
      avgLatency: 'Avg Latency',
      errorRate: 'Error Rate',
      llmCost: 'LLM Cost (24h)',
      cpuUsage: 'CPU Usage',
      memoryUsage: 'Memory Usage',
      diskUsage: 'Disk Usage',
      networkTraffic: 'Network Traffic',
    },
    charts: {
      last1h: 'Last 1h',
      last6h: 'Last 6h',
      last24h: 'Last 24h',
      last7d: 'Last 7 days',
      cpuUsageTitle: 'CPU Usage (%)',
      memoryUsageTitle: 'Memory Usage (MB)',
      apiRequestsTitle: 'API Requests (req/s)',
      networkTitle: 'Network Traffic (MB/s)',
    },
  },
} as const;

export type Locale = keyof typeof infrastructurei18n;

/**
 * Get translated strings for infrastructure monitoring
 * @param locale Current locale (default: 'it')
 */
export function getInfrastructureI18n(locale: Locale = 'it') {
  // Safe access with known keys - locale is typed Locale union ('it' | 'en')
  return locale in infrastructurei18n ? infrastructurei18n[locale] : infrastructurei18n.it;
}
