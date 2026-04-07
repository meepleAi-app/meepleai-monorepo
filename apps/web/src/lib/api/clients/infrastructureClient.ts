/**
 * Infrastructure API Client (AI Infrastructure Dashboard)
 *
 * Client for admin infrastructure monitoring endpoints including
 * service status, dependencies, pipeline testing, and configuration.
 */

import { HttpClient } from '../core/httpClient';

// ============================================================================
// Types
// ============================================================================

export interface AiServiceStatus {
  name: string;
  displayName: string;
  type: 'ai' | 'infra';
  status: 'Healthy' | 'Degraded' | 'Down' | 'Restarting' | 'Unknown';
  uptime: string;
  avgLatencyMs: number;
  errorRate24h: number;
  lastCheckedAt: string;
  canRestart: boolean;
  cooldownRemainingSeconds: number | null;
}

export interface AiServicesStatusResponse {
  services: AiServiceStatus[];
}

export interface ServiceDependency {
  name: string;
  displayName: string;
  status: string;
  latencyMs: number;
}

export interface ServiceDependenciesResponse {
  serviceName: string;
  dependencies: ServiceDependency[];
}

export interface PipelineHop {
  serviceName: string;
  displayName: string;
  status: string;
  latencyMs: number;
  error: string | null;
}

export interface PipelineTestResponse {
  success: boolean;
  hops: PipelineHop[];
  totalLatencyMs: number;
}

export interface ServiceConfigParam {
  key: string;
  displayName: string;
  value: string;
  type: 'string' | 'int' | 'enum';
  options: string[] | null;
  minValue: number | null;
  maxValue: number | null;
}

export interface ServiceConfigResponse {
  serviceName: string;
  parameters: ServiceConfigParam[];
}

export interface RestartResponse {
  success: boolean;
  serviceName: string;
  cooldownExpiresAt: string | null;
}

export interface HealthCheckResponse {
  serviceName: string;
  status: string;
  details: string | null;
  latencyMs: number;
}

export interface ConfigUpdateResponse {
  serviceName: string;
  updatedParams: string[];
}

// ============================================================================
// Client Interface
// ============================================================================

export interface InfrastructureClient {
  /** Get status of all AI/infra services */
  getServices(): Promise<AiServicesStatusResponse>;

  /** Get dependency tree for a specific service */
  getServiceDependencies(name: string): Promise<ServiceDependenciesResponse>;

  /** Get configurable parameters for a service */
  getServiceConfig(name: string): Promise<ServiceConfigResponse>;

  /** Test full RAG pipeline connectivity */
  testPipeline(): Promise<PipelineTestResponse>;

  /** Restart a specific service (with cooldown) */
  restartService(name: string): Promise<RestartResponse>;

  /** Trigger a health check for a specific service */
  triggerHealthCheck(name: string): Promise<HealthCheckResponse>;

  /** Update configuration parameters for a service */
  updateServiceConfig(name: string, params: Record<string, string>): Promise<ConfigUpdateResponse>;
}

// ============================================================================
// Client Factory
// ============================================================================

export interface InfrastructureClientConfig {
  httpClient: HttpClient;
}

/**
 * Create Infrastructure API client
 */
export function createInfrastructureClient(
  config: InfrastructureClientConfig
): InfrastructureClient {
  const { httpClient } = config;
  const base = '/api/v1/admin/infrastructure';

  return {
    async getServices(): Promise<AiServicesStatusResponse> {
      const response = await httpClient.get<AiServicesStatusResponse>(`${base}/services`);
      if (!response) {
        return { services: [] };
      }
      return response;
    },

    async getServiceDependencies(name: string): Promise<ServiceDependenciesResponse> {
      const response = await httpClient.get<ServiceDependenciesResponse>(
        `${base}/services/${encodeURIComponent(name)}/dependencies`
      );
      if (!response) {
        return { serviceName: name, dependencies: [] };
      }
      return response;
    },

    async getServiceConfig(name: string): Promise<ServiceConfigResponse> {
      const response = await httpClient.get<ServiceConfigResponse>(
        `${base}/services/${encodeURIComponent(name)}/config`
      );
      if (!response) {
        return { serviceName: name, parameters: [] };
      }
      return response;
    },

    async testPipeline(): Promise<PipelineTestResponse> {
      const response = await httpClient.get<PipelineTestResponse>(`${base}/pipeline/test`);
      if (!response) {
        return { success: false, hops: [], totalLatencyMs: 0 };
      }
      return response;
    },

    async restartService(name: string): Promise<RestartResponse> {
      const response = await httpClient.post<RestartResponse>(
        `${base}/services/${encodeURIComponent(name)}/restart`
      );
      if (!response) {
        return { success: false, serviceName: name, cooldownExpiresAt: null };
      }
      return response;
    },

    async triggerHealthCheck(name: string): Promise<HealthCheckResponse> {
      const response = await httpClient.post<HealthCheckResponse>(
        `${base}/services/${encodeURIComponent(name)}/health-check`
      );
      if (!response) {
        return { serviceName: name, status: 'Unknown', details: null, latencyMs: 0 };
      }
      return response;
    },

    async updateServiceConfig(
      name: string,
      params: Record<string, string>
    ): Promise<ConfigUpdateResponse> {
      const response = await httpClient.put<ConfigUpdateResponse>(
        `${base}/services/${encodeURIComponent(name)}/config`,
        params
      );
      if (!response) {
        return { serviceName: name, updatedParams: [] };
      }
      return response;
    },
  };
}
