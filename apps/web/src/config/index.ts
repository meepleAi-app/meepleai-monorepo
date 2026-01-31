/**
 * Configuration Module Exports
 *
 * Centralized type-safe configuration for the MeepleAI frontend.
 * All magic numbers and hardcoded strings are defined here for easy maintenance.
 *
 * Usage:
 * ```typescript
 * import { CHAT_CONFIG, UI_CONFIG, API_CONFIG } from '@/config';
 *
 * const maxThreads = CHAT_CONFIG.MAX_THREADS_PER_GAME;
 * const toastDuration = UI_CONFIG.TOAST_DURATION_MS;
 * const retryAttempts = API_CONFIG.RETRY_MAX_ATTEMPTS;
 * ```
 */

export { CHAT_CONFIG, type ChatConfigKey } from './chat';
export { UI_CONFIG, type UiConfigKey } from './ui';
export { API_CONFIG, type ApiConfigKey } from './api';
export {
  GRAFANA_DASHBOARDS,
  getGrafanaBaseUrl,
  buildGrafanaEmbedUrl,
  getDashboardById,
  DEFAULT_DASHBOARD,
  type GrafanaDashboard,
} from './grafana-dashboards';
