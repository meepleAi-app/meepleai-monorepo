/**
 * Infrastructure Monitoring Client Component - Storybook Stories
 *
 * Issue #899: Visual testing for infrastructure monitoring page
 *
 * Test Coverage:
 * - All 3 tabs (Services, Charts, Grafana)
 * - Loading states
 * - Error states (including circuit breaker)
 * - Different filter modes
 * - Empty state
 * - Mobile/tablet/desktop viewports
 */

import { api } from '@/lib/api';
import type { InfrastructureDetails } from '@/lib/api';

import { InfrastructureClient } from './infrastructure-client';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof InfrastructureClient> = {
  title: 'Admin/Pages/InfrastructureClient',
  component: InfrastructureClient,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Complete infrastructure monitoring dashboard with service health, metrics charts, and Grafana embeds. Issue #899.',
      },
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof InfrastructureClient>;

// Mock data
const mockHealthyInfrastructure: InfrastructureDetails = {
  overall: {
    state: 'Healthy',
    totalServices: 6,
    healthyServices: 6,
    degradedServices: 0,
    unhealthyServices: 0,
    checkedAt: new Date().toISOString(),
  },
  services: [
    {
      serviceName: 'postgres',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0150000',
    },
    {
      serviceName: 'redis',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0020000',
    },
    {
      serviceName: 'qdrant',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0080000',
    },
    {
      serviceName: 'n8n',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0120000',
    },
    {
      serviceName: 'prometheus',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0050000',
    },
    {
      serviceName: 'grafana',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0090000',
    },
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 15234,
    avgLatencyMs: 125.4,
    errorRate: 0.012,
    llmCostLast24h: 3.45,
  },
};

const mockDegradedInfrastructure: InfrastructureDetails = {
  overall: {
    state: 'Degraded',
    totalServices: 6,
    healthyServices: 4,
    degradedServices: 2,
    unhealthyServices: 0,
    checkedAt: new Date().toISOString(),
  },
  services: [
    ...mockHealthyInfrastructure.services.slice(0, 4),
    {
      serviceName: 'n8n',
      state: 'Degraded',
      errorMessage: 'High response time detected',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:02.5000000',
    },
    {
      serviceName: 'hyperdx',
      state: 'Degraded',
      errorMessage: 'Connection intermittent',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:01.8000000',
    },
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 12890,
    avgLatencyMs: 284.7,
    errorRate: 0.035,
    llmCostLast24h: 4.12,
  },
};

const mockUnhealthyInfrastructure: InfrastructureDetails = {
  overall: {
    state: 'Unhealthy',
    totalServices: 6,
    healthyServices: 3,
    degradedServices: 1,
    unhealthyServices: 2,
    checkedAt: new Date().toISOString(),
  },
  services: [
    ...mockHealthyInfrastructure.services.slice(0, 3),
    {
      serviceName: 'redis',
      state: 'Degraded',
      errorMessage: 'Memory usage at 85%',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0450000',
    },
    {
      serviceName: 'qdrant',
      state: 'Unhealthy',
      errorMessage: 'Connection refused: ECONNREFUSED 127.0.0.1:6333',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:05.0000000',
    },
    {
      serviceName: 'n8n',
      state: 'Unhealthy',
      errorMessage: 'Service unreachable',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:10.0000000',
    },
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 8456,
    avgLatencyMs: 512.3,
    errorRate: 0.089,
    llmCostLast24h: 5.78,
  },
};

// Story: Healthy State
export const HealthyState: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: mockHealthyInfrastructure,
      },
    ],
  },
  beforeEach: () => {
    // Mock API call
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(mockHealthyInfrastructure);
  },
};

// Story: Degraded State
export const DegradedState: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: mockDegradedInfrastructure,
      },
    ],
  },
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(mockDegradedInfrastructure);
  },
};

// Story: Unhealthy State
export const UnhealthyState: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: mockUnhealthyInfrastructure,
      },
    ],
  },
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(mockUnhealthyInfrastructure);
  },
};

// Story: Loading State
export const LoadingState: Story = {
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );
  },
};

// Story: Error State
export const ErrorState: Story = {
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockRejectedValue(new Error('Network error'));
  },
};

// Story: Circuit Breaker Open
export const CircuitBreakerOpen: Story = {
  beforeEach: () => {
    // Simulate 5+ consecutive failures
    let callCount = 0;
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockImplementation(() => {
      callCount++;
      return Promise.reject(new Error(`Network error (attempt ${callCount})`));
    });
  },
};

// Story: Empty State (No Services)
export const EmptyState: Story = {
  beforeEach: () => {
    const emptyData: InfrastructureDetails = {
      overall: {
        state: 'Healthy',
        totalServices: 0,
        healthyServices: 0,
        degradedServices: 0,
        unhealthyServices: 0,
        checkedAt: new Date().toISOString(),
      },
      services: [],
      prometheusMetrics: {
        apiRequestsLast24h: 0,
        avgLatencyMs: 0,
        errorRate: 0,
        llmCostLast24h: 0,
      },
    };
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(emptyData);
  },
};

// Story: Mobile Viewport
export const MobileView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: mockHealthyInfrastructure,
      },
    ],
  },
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(mockHealthyInfrastructure);
  },
};

// Story: Tablet Viewport
export const TabletView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: mockHealthyInfrastructure,
      },
    ],
  },
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(mockHealthyInfrastructure);
  },
};

// Story: Dark Mode
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: mockHealthyInfrastructure,
      },
    ],
  },
  beforeEach: () => {
    vi.spyOn(api.admin, 'getInfrastructureDetails').mockResolvedValue(mockHealthyInfrastructure);
    document.documentElement.classList.add('dark');
  },
};
