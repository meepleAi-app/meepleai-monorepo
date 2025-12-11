/**
 * Test Utilities for Infrastructure Components - Issue #900
 */

import { type ReactElement } from 'react';
import { render, type RenderOptions } from '@testing-library/react';
import type { InfrastructureDetails, ServiceHealthStatus, HealthState } from '@/lib/api';

export const createMockService = (
  name: string,
  state: HealthState,
  responseMs: number,
  errorMsg?: string
): ServiceHealthStatus => ({
  serviceName: name,
  state,
  errorMessage: errorMsg || null,
  checkedAt: new Date('2024-12-11T10:00:00Z').toISOString(),
  responseTime: `00:00:00.${responseMs.toString().padStart(7, '0')}`,
});

export const createHealthyInfraData = (): InfrastructureDetails => ({
  overall: {
    state: 'Healthy',
    totalServices: 4,
    healthyServices: 4,
    degradedServices: 0,
    unhealthyServices: 0,
    checkedAt: new Date('2024-12-11T10:00:00Z').toISOString(),
  },
  services: [
    createMockService('postgres', 'Healthy', 15000),
    createMockService('redis', 'Healthy', 2000),
    createMockService('qdrant', 'Healthy', 8000),
    createMockService('n8n', 'Healthy', 12000),
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 15234,
    avgLatencyMs: 125.4,
    errorRate: 0.012,
    llmCostLast24h: 3.45,
  },
});

export const createDegradedInfraData = (): InfrastructureDetails => ({
  overall: {
    state: 'Degraded',
    totalServices: 4,
    healthyServices: 2,
    degradedServices: 1,
    unhealthyServices: 1,
    checkedAt: new Date('2024-12-11T10:00:00Z').toISOString(),
  },
  services: [
    createMockService('postgres', 'Healthy', 15000),
    createMockService('redis', 'Healthy', 2000),
    createMockService('qdrant', 'Degraded', 2000000, 'High latency detected'),
    createMockService('n8n', 'Unhealthy', 5000000, 'Connection refused'),
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 8432,
    avgLatencyMs: 456.2,
    errorRate: 0.087,
    llmCostLast24h: 5.67,
  },
});

export function renderWithProviders(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
  return render(ui, { ...options });
}

export const TEST_TIMEOUTS = {
  FAST: 1000,
  STANDARD: 3000,
  SLOW: 5000,
  INTEGRATION: 10000,
} as const;
