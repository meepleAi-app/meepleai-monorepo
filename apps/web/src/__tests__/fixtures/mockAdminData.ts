/**
 * Mock Admin Data Fixtures (Issue #2914)
 *
 * Factory functions for admin dashboard test data
 */

import type {
  DashboardStats,
  DashboardMetrics,
  TimeSeriesDataPoint,
  RecentActivityDto,
  ActivityEvent,
  InfrastructureDetails,
  AdminStats,
} from '@/lib/api';

/**
 * Create mock TimeSeriesDataPoint
 */
export function createMockTimeSeriesDataPoint(
  overrides?: Partial<TimeSeriesDataPoint>
): TimeSeriesDataPoint {
  return {
    date: new Date('2026-01-23T00:00:00Z').toISOString(),
    count: 100,
    averageValue: 50.5,
    ...overrides,
  };
}

/**
 * Create mock trend data (7 days by default)
 */
export function createMockTrendData(days: number = 7): TimeSeriesDataPoint[] {
  return Array.from({ length: days }, (_, i) => {
    const date = new Date('2026-01-23T00:00:00Z');
    date.setDate(date.getDate() - (days - 1 - i));
    return createMockTimeSeriesDataPoint({
      date: date.toISOString(),
      count: Math.floor(Math.random() * 200) + 50,
      averageValue: Math.random() * 100,
    });
  });
}

/**
 * Create mock DashboardMetrics
 */
export function createMockDashboardMetrics(
  overrides?: Partial<DashboardMetrics>
): DashboardMetrics {
  return {
    totalUsers: 2847,
    activeSessions: 156,
    apiRequestsToday: 8492,
    totalPdfDocuments: 567,
    totalChatMessages: 10000,
    averageConfidenceScore: 0.85,
    totalRagRequests: 5000,
    totalTokensUsed: 2500000,
    totalGames: 1234,
    apiRequests7d: 45000,
    apiRequests30d: 150000,
    averageLatency24h: 120,
    averageLatency7d: 115,
    errorRate24h: 0.02,
    activeAlerts: 2,
    resolvedAlerts: 50,
    // Issue #3694: Extended KPIs for Enterprise Admin Dashboard
    tokenBalanceEur: 450.75,
    tokenLimitEur: 1000.0,
    dbStorageGb: 2.5,
    dbStorageLimitGb: 10.0,
    dbGrowthMbPerDay: 15.3,
    cacheHitRatePercent: 87.5,
    cacheHitRateTrendPercent: 2.1,
    ...overrides,
  };
}

/**
 * Create mock DashboardStats (analytics data)
 */
export function createMockDashboardStats(
  overrides?: Partial<DashboardStats>
): DashboardStats {
  return {
    metrics: createMockDashboardMetrics(overrides?.metrics),
    userTrend: overrides?.userTrend || createMockTrendData(7),
    sessionTrend: overrides?.sessionTrend || createMockTrendData(7),
    apiRequestTrend: overrides?.apiRequestTrend || createMockTrendData(7),
    pdfUploadTrend: overrides?.pdfUploadTrend || createMockTrendData(7),
    chatMessageTrend: overrides?.chatMessageTrend || createMockTrendData(7),
    generatedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
    ...overrides,
  };
}

/**
 * Create mock ActivityEvent
 */
export function createMockActivityEvent(
  overrides?: Partial<ActivityEvent>
): ActivityEvent {
  return {
    id: `event-${Math.random().toString(36).substring(7)}`,
    eventType: 'UserLogin',
    description: 'User logged in successfully',
    userId: 'user-123',
    userEmail: 'alice@example.com',
    entityId: null,
    entityType: null,
    timestamp: new Date('2026-01-23T10:30:00Z').toISOString(),
    severity: 'Info',
    ...overrides,
  };
}

/**
 * Create mock RecentActivityDto (activity feed)
 */
export function createMockRecentActivity(
  overrides?: Partial<RecentActivityDto>
): RecentActivityDto {
  return {
    events: [
      createMockActivityEvent({
        id: 'event-1',
        eventType: 'UserRegistered',
        description: 'New user registered',
        userEmail: 'alice@example.com',
        timestamp: new Date('2026-01-23T10:30:00Z').toISOString(),
      }),
      createMockActivityEvent({
        id: 'event-2',
        eventType: 'PdfUploaded',
        description: 'PDF document uploaded',
        userId: 'user-456',
        userEmail: 'bob@example.com',
        entityId: 'doc-789',
        entityType: 'Document',
        timestamp: new Date('2026-01-23T10:25:00Z').toISOString(),
      }),
      createMockActivityEvent({
        id: 'event-3',
        eventType: 'GameAdded',
        description: 'New game added to catalog',
        userId: 'user-789',
        userEmail: 'charlie@example.com',
        entityId: 'game-123',
        entityType: 'Game',
        timestamp: new Date('2026-01-23T10:20:00Z').toISOString(),
      }),
    ],
    totalCount: 150,
    generatedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
    ...overrides,
  };
}

/**
 * Create mock InfrastructureDetails (system health)
 */
export function createMockInfrastructureDetails(
  overrides?: Partial<InfrastructureDetails>
): InfrastructureDetails {
  return {
    overall: {
      state: 'Healthy',
      totalServices: 4,
      healthyServices: 4,
      degradedServices: 0,
      unhealthyServices: 0,
      checkedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
    },
    services: [
      {
        serviceName: 'API',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
        responseTimeMs: 15,
      },
      {
        serviceName: 'PostgreSQL',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
        responseTimeMs: 5,
      },
      {
        serviceName: 'Redis',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
        responseTimeMs: 2,
      },
      {
        serviceName: 'Qdrant',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date('2026-01-23T12:00:00Z').toISOString(),
        responseTimeMs: 8,
      },
    ],
    prometheusMetrics: {
      apiRequestsLast24h: 8492,
      avgLatencyMs: 120,
      errorRate: 0.02,
      llmCostLast24h: 45.5,
    },
    ...overrides,
  };
}

/**
 * Create mock AdminStats (legacy /api/v1/admin/stats)
 */
export function createMockAdminStats(overrides?: Partial<AdminStats>): AdminStats {
  return {
    totalRequests: 8492,
    avgLatencyMs: 120,
    totalTokens: 2500000,
    successRate: 0.98,
    endpointCounts: {
      '/api/v1/chat': 3500,
      '/api/v1/games': 2000,
      '/api/v1/documents': 1500,
      '/api/v1/rag': 1492,
    },
    feedbackCounts: {
      positive: 450,
      negative: 50,
      neutral: 200,
    },
    totalFeedback: 700,
    ...overrides,
  };
}

/**
 * Create mock error response for API failures
 */
export function createMockErrorResponse(
  status: number = 500,
  message: string = 'Internal server error'
) {
  return {
    error: message,
    status,
    timestamp: new Date().toISOString(),
  };
}

/**
 * Create mock network timeout simulation
 */
export function createNetworkDelay(ms: number = 100): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
