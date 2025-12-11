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
      qdrant: 'Qdrant',
      'qdrant-collection': 'Qdrant Collection',
      n8n: 'n8n',
      hyperdx: 'HyperDX',
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
  },
  en: {
    // Service names
    services: {
      postgres: 'PostgreSQL',
      redis: 'Redis',
      qdrant: 'Qdrant',
      'qdrant-collection': 'Qdrant Collection',
      n8n: 'n8n',
      hyperdx: 'HyperDX',
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
  },
} as const;

export type Locale = keyof typeof infrastructurei18n;

/**
 * Get translated strings for infrastructure monitoring
 * @param locale Current locale (default: 'it')
 */
export function getInfrastructureI18n(locale: Locale = 'it') {
  // Safe access with known keys - locale is typed Locale union ('it' | 'en')
  return locale in infrastructurei18n
    ? // eslint-disable-next-line security/detect-object-injection
      infrastructurei18n[locale]
    : infrastructurei18n.it;
}
