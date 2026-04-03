/**
 * MSW handlers for admin dashboard endpoints (browser-safe)
 * Covers: /api/v1/admin/*
 * Data is inlined (no @/lib/api type imports) to stay browser-safe.
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const dashboardStats = {
  metrics: {
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
    tokenBalanceEur: 450.75,
    tokenLimitEur: 1000.0,
    dbStorageGb: 2.5,
    dbStorageLimitGb: 10.0,
    dbGrowthMbPerDay: 15.3,
    cacheHitRatePercent: 87.5,
    cacheHitRateTrendPercent: 2.1,
  },
  trends: Array.from({ length: 7 }, (_, i) => {
    const d = new Date('2026-04-02T00:00:00Z');
    d.setDate(d.getDate() - (6 - i));
    return { date: d.toISOString(), count: Math.floor(100 + i * 20), averageValue: 50 + i * 5 };
  }),
};

const recentActivity = {
  events: [
    {
      id: 'ev-1',
      type: 'user_registered',
      description: 'New user registered',
      timestamp: new Date(Date.now() - 60000).toISOString(),
      severity: 'info',
    },
    {
      id: 'ev-2',
      type: 'pdf_processed',
      description: 'PDF processed successfully',
      timestamp: new Date(Date.now() - 120000).toISOString(),
      severity: 'success',
    },
    {
      id: 'ev-3',
      type: 'api_error',
      description: 'API rate limit hit',
      timestamp: new Date(Date.now() - 300000).toISOString(),
      severity: 'warning',
    },
  ],
  totalCount: 3,
};

const infrastructureDetails = {
  services: [
    { name: 'API', status: 'healthy', uptime: 99.9, latencyMs: 120 },
    { name: 'Database', status: 'healthy', uptime: 99.95, latencyMs: 5 },
    { name: 'Redis', status: 'healthy', uptime: 100, latencyMs: 1 },
    { name: 'Embedding Service', status: 'healthy', uptime: 98.5, latencyMs: 250 },
  ],
};

const adminStats = {
  totalUsers: 2847,
  totalGames: 1234,
  totalDocuments: 567,
  totalSessions: 890,
  totalChatMessages: 10000,
};

export const adminHandlers = [
  http.get(`${API_BASE}/api/v1/admin/stats`, () => HttpResponse.json(adminStats)),
  http.get(`${API_BASE}/api/v1/admin/analytics`, () => HttpResponse.json(dashboardStats)),
  http.get(`${API_BASE}/api/v1/admin/activity`, () => HttpResponse.json(recentActivity)),
  http.get(`${API_BASE}/api/v1/admin/infrastructure`, () =>
    HttpResponse.json(infrastructureDetails)
  ),

  http.get(`${API_BASE}/api/v1/admin/users`, ({ request }) => {
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const users = Array.from({ length: 5 }, (_, i) => ({
      id: `user-${i + 1}`,
      email: `user${i + 1}@meepleai.dev`,
      displayName: `User ${i + 1}`,
      role: i === 0 ? 'Admin' : 'User',
      createdAt: new Date(Date.now() - i * 86400000).toISOString(),
      isActive: true,
    }));
    return HttpResponse.json({
      items: users,
      totalCount: users.length,
      page,
      pageSize,
      totalPages: 1,
    });
  }),

  http.get(`${API_BASE}/api/v1/admin/content`, () => {
    return HttpResponse.json({ totalDocuments: 567, pendingDocuments: 12, failedDocuments: 3 });
  }),
];
