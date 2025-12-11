/**
 * Grafana Dashboard Configuration
 * Issue #901 - Grafana embed iframe setup
 *
 * Configures available Grafana dashboards for embedding in the Infrastructure page.
 * Dashboard UIDs must match the actual UIDs in Grafana.
 */

export interface GrafanaDashboard {
  id: string;
  uid: string; // Grafana dashboard UID
  name: {
    en: string;
    it: string;
  };
  description: {
    en: string;
    it: string;
  };
  icon?: string;
}

/**
 * Available Grafana dashboards for embedding
 * Priority order: infrastructure → llm-cost → api-performance → rag
 */
export const GRAFANA_DASHBOARDS: GrafanaDashboard[] = [
  {
    id: 'infrastructure',
    uid: 'infrastructure-monitoring',
    name: {
      en: 'Infrastructure Monitoring',
      it: 'Monitoraggio Infrastruttura',
    },
    description: {
      en: 'Container metrics (cAdvisor), host metrics (node-exporter), CPU, memory, disk, network',
      it: 'Metriche container (cAdvisor), metriche host (node-exporter), CPU, memoria, disco, rete',
    },
    icon: 'Server',
  },
  {
    id: 'llm-cost',
    uid: 'llm-cost-monitoring',
    name: {
      en: 'LLM Cost Tracking',
      it: 'Tracciamento Costi LLM',
    },
    description: {
      en: 'Token usage by model and provider, cost per request, budget tracking',
      it: 'Utilizzo token per modello e provider, costo per richiesta, tracciamento budget',
    },
    icon: 'DollarSign',
  },
  {
    id: 'api-performance',
    uid: 'api-performance',
    name: {
      en: 'API Performance',
      it: 'Performance API',
    },
    description: {
      en: 'Request rate, latency (p50/p95/p99), error rate, status codes',
      it: 'Rate richieste, latenza (p50/p95/p99), tasso errori, codici stato',
    },
    icon: 'Activity',
  },
  {
    id: 'rag-operations',
    uid: 'ai-rag-operations',
    name: {
      en: 'RAG Operations',
      it: 'Operazioni RAG',
    },
    description: {
      en: 'RAG request rate, duration, token usage, confidence scores, vector search latency',
      it: 'Rate richieste RAG, durata, utilizzo token, punteggi confidenza, latenza ricerca vettoriale',
    },
    icon: 'Brain',
  },
];

/**
 * Get Grafana base URL from environment or default
 */
export const getGrafanaBaseUrl = (): string => {
  return process.env.NEXT_PUBLIC_GRAFANA_URL || 'http://localhost:3001';
};

/**
 * Build Grafana dashboard embed URL with parameters
 * @param dashboard Dashboard configuration
 * @param options Embed options (theme, refresh, time range)
 */
export const buildGrafanaEmbedUrl = (
  dashboard: GrafanaDashboard,
  options?: {
    theme?: 'light' | 'dark';
    refresh?: string; // e.g., '30s', '1m', '5m'
    from?: string; // e.g., 'now-1h', 'now-6h'
    to?: string; // e.g., 'now'
    kiosk?: boolean; // Kiosk mode (hide Grafana UI)
  }
): string => {
  const baseUrl = getGrafanaBaseUrl();
  const params = new URLSearchParams();

  // Embed mode (hide top nav)
  params.set('kiosk', options?.kiosk !== false ? 'tv' : '');

  // Theme
  if (options?.theme) {
    params.set('theme', options.theme);
  }

  // Auto-refresh
  if (options?.refresh) {
    params.set('refresh', options.refresh);
  }

  // Time range
  if (options?.from) {
    params.set('from', options.from);
  }
  if (options?.to) {
    params.set('to', options.to);
  }

  return `${baseUrl}/d/${dashboard.uid}?${params.toString()}`;
};

/**
 * Get dashboard by ID
 */
export const getDashboardById = (id: string): GrafanaDashboard | undefined => {
  return GRAFANA_DASHBOARDS.find(d => d.id === id);
};

/**
 * Default dashboard (infrastructure monitoring)
 */
export const DEFAULT_DASHBOARD = GRAFANA_DASHBOARDS[0];
