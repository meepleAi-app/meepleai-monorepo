'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ExportButton } from '@/components/admin/ExportButton';
import {
  exportTestingMetricsToCSV,
  exportTestingMetricsToPDF,
  type TestingMetrics,
} from '@/lib/utils/export';
import { api } from '@/lib/api';
import type {
  AccessibilityMetrics,
  PerformanceMetrics,
  E2EMetrics,
} from '@/lib/api/schemas/testing.schemas';

// Mock data based on completed Issues #841, #842, #843
const MOCK_DATA = {
  accessibility: {
    lighthouseScore: 98,
    axeViolations: 2,
    wcagCompliance: {
      levelA: 100,
      levelAA: 96,
      levelAAA: 85,
    },
    testedPages: 12,
    criticalIssues: [
      { page: '/admin/users', issue: 'Missing alt text on avatar image', severity: 'medium' },
      {
        page: '/settings',
        issue: 'Insufficient color contrast on disabled button',
        severity: 'low',
      },
    ],
    lastRun: '2025-12-10T14:30:00Z',
  },
  performance: {
    lighthouseScore: 92,
    coreWebVitals: {
      lcp: 1.8, // seconds
      fid: 45, // milliseconds
      cls: 0.05,
    },
    budgetStatus: {
      js: { current: 245, budget: 300, unit: 'KB' },
      css: { current: 48, budget: 75, unit: 'KB' },
      images: { current: 890, budget: 1024, unit: 'KB' },
    },
    slowestPages: [
      { page: '/admin/analytics', loadTime: 2.4 },
      { page: '/admin/infrastructure', loadTime: 2.1 },
      { page: '/chat', loadTime: 1.9 },
    ],
    lastRun: '2025-12-10T15:00:00Z',
  },
  e2e: {
    coverage: 82, // percentage
    passRate: 97.5, // percentage
    flakyRate: 3.2, // percentage
    totalTests: 156,
    executionTime: 8.5, // minutes
    criticalJourneys: [
      { name: 'User Login Flow', status: 'pass', duration: 12.3 },
      { name: 'PDF Upload & Processing', status: 'pass', duration: 24.7 },
      { name: 'RAG Chat Session', status: 'pass', duration: 18.9 },
      { name: 'Admin Dashboard Access', status: 'pass', duration: 9.1 },
      { name: 'API Key Generation', status: 'flaky', duration: 15.4 },
    ],
    lastRun: '2025-12-10T16:00:00Z',
  },
};

// Helper function to format date
const formatDate = (isoString: string) => {
  return new Date(isoString).toLocaleString('en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
};

// Helper function to get status badge color
const getStatusColor = (status: string) => {
  switch (status) {
    case 'pass':
      return 'bg-green-500';
    case 'flaky':
      return 'bg-yellow-500';
    case 'fail':
      return 'bg-red-500';
    default:
      return 'bg-gray-500';
  }
};

// Helper function to get severity badge color
const getSeverityColor = (severity: string) => {
  switch (severity) {
    case 'high':
      return 'destructive';
    case 'medium':
      return 'default';
    case 'low':
      return 'secondary';
    default:
      return 'outline';
  }
};

// Loading skeleton component
function LoadingSkeleton() {
  return (
    <div className="space-y-6">
      <div className="animate-pulse">
        <div className="grid gap-4 md:grid-cols-3 mb-6">
          {[1, 2, 3].map(i => (
            <div key={i} className="h-32 bg-gray-200 rounded"></div>
          ))}
        </div>
        <div className="grid gap-4">
          {[1, 2].map(i => (
            <div key={i} className="h-64 bg-gray-200 rounded"></div>
          ))}
        </div>
      </div>
    </div>
  );
}

// Error display component
function ErrorDisplay({ error, onRetry }: { error: Error; onRetry: () => void }) {
  return (
    <Card className="border-red-200 bg-red-50">
      <CardHeader>
        <CardTitle className="text-red-900">Failed to Load Testing Metrics</CardTitle>
        <CardDescription className="text-red-700">{error.message}</CardDescription>
      </CardHeader>
      <CardContent>
        <button
          onClick={onRetry}
          className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
        >
          Retry
        </button>
      </CardContent>
    </Card>
  );
}

export function TestingDashboardClient() {
  const [activeTab, setActiveTab] = useState('accessibility');

  // Fetch accessibility metrics with 30s polling
  const {
    data: accessibilityData,
    isLoading: accessibilityLoading,
    error: accessibilityError,
    refetch: refetchAccessibility,
  } = useQuery<AccessibilityMetrics>({
    queryKey: ['admin', 'testing', 'accessibility'],
    queryFn: () => api.admin.getAccessibilityMetrics(),
    refetchInterval: 30000, // 30s polling
    retry: 2,
    staleTime: 25000, // Consider data stale after 25s
  });

  // Fetch performance metrics with 30s polling
  const {
    data: performanceData,
    isLoading: performanceLoading,
    error: performanceError,
    refetch: refetchPerformance,
  } = useQuery<PerformanceMetrics>({
    queryKey: ['admin', 'testing', 'performance'],
    queryFn: () => api.admin.getPerformanceMetrics(),
    refetchInterval: 30000, // 30s polling
    retry: 2,
    staleTime: 25000,
  });

  // Fetch E2E metrics with 30s polling
  const {
    data: e2eData,
    isLoading: e2eLoading,
    error: e2eError,
    refetch: refetchE2E,
  } = useQuery<E2EMetrics>({
    queryKey: ['admin', 'testing', 'e2e'],
    queryFn: () => api.admin.getE2EMetrics(),
    refetchInterval: 30000, // 30s polling
    retry: 2,
    staleTime: 25000,
  });

  // Export handlers - use real data when available, fallback to MOCK_DATA
  const handleExportCSV = () => {
    const metricsToExport: TestingMetrics = {
      accessibility: accessibilityData || MOCK_DATA.accessibility,
      performance: performanceData || MOCK_DATA.performance,
      e2e: e2eData || MOCK_DATA.e2e,
    };
    exportTestingMetricsToCSV(metricsToExport);
  };

  const handleExportPDF = async () => {
    await exportTestingMetricsToPDF('testing-dashboard-content');
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Testing Dashboard</h1>
          <p className="text-muted-foreground mt-2">
            Automated testing results for accessibility, performance, and E2E coverage
          </p>
        </div>
        <ExportButton onExportCSV={handleExportCSV} onExportPDF={handleExportPDF} />
      </div>

      {/* Tabs Container - Wrapped with ID for PDF export */}
      <div id="testing-dashboard-content">
        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="accessibility">
              Accessibility
              {accessibilityLoading && <span className="ml-2 text-xs">(loading...)</span>}
            </TabsTrigger>
            <TabsTrigger value="performance">
              Performance
              {performanceLoading && <span className="ml-2 text-xs">(loading...)</span>}
            </TabsTrigger>
            <TabsTrigger value="e2e">
              E2E Coverage
              {e2eLoading && <span className="ml-2 text-xs">(loading...)</span>}
            </TabsTrigger>
          </TabsList>

          {/* Accessibility Tab */}
          <TabsContent value="accessibility" className="space-y-4">
            {accessibilityLoading ? (
              <LoadingSkeleton />
            ) : accessibilityError ? (
              <ErrorDisplay error={accessibilityError as Error} onRetry={refetchAccessibility} />
            ) : accessibilityData ? (
              <>
                <div className="grid gap-4 md:grid-cols-3">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Lighthouse Score</CardDescription>
                      <CardTitle className="text-4xl">
                        {accessibilityData.lighthouseScore}
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Progress value={accessibilityData.lighthouseScore} className="mt-2" />
                      <p className="text-xs text-muted-foreground mt-2">Target: ≥95</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>axe Violations</CardDescription>
                      <CardTitle className="text-4xl">{accessibilityData.axeViolations}</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge
                        variant={accessibilityData.axeViolations === 0 ? 'default' : 'destructive'}
                      >
                        {accessibilityData.axeViolations === 0 ? 'No Issues' : 'Issues Found'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: 0</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Pages Tested</CardDescription>
                      <CardTitle className="text-4xl">{accessibilityData.testedPages}</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <p className="text-xs text-muted-foreground mt-2">
                        Last run: {formatDate(accessibilityData.lastRun)}
                      </p>
                    </CardContent>
                  </Card>
                </div>

                <Card>
                  <CardHeader>
                    <CardTitle>WCAG 2.x Compliance</CardTitle>
                    <CardDescription>Compliance levels for accessibility standards</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div>
                      <div className="flex justify-between text-sm mb-2">
                        <span>Level A</span>
                        <span className="font-medium">
                          {accessibilityData.wcagCompliance.levelA}%
                        </span>
                      </div>
                      <Progress value={accessibilityData.wcagCompliance.levelA} />
                    </div>
                    <div>
                      <div className="flex justify-between text-sm mb-2">
                        <span>Level AA</span>
                        <span className="font-medium">
                          {accessibilityData.wcagCompliance.levelAA}%
                        </span>
                      </div>
                      <Progress value={accessibilityData.wcagCompliance.levelAA} />
                    </div>
                    <div>
                      <div className="flex justify-between text-sm mb-2">
                        <span>Level AAA</span>
                        <span className="font-medium">
                          {accessibilityData.wcagCompliance.levelAAA}%
                        </span>
                      </div>
                      <Progress value={accessibilityData.wcagCompliance.levelAAA} />
                    </div>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Critical Issues</CardTitle>
                    <CardDescription>
                      Outstanding accessibility violations requiring attention
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Page</TableHead>
                          <TableHead>Issue</TableHead>
                          <TableHead>Severity</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {accessibilityData.criticalIssues.map((issue, idx) => (
                          <TableRow key={idx}>
                            <TableCell className="font-mono text-sm">{issue.page}</TableCell>
                            <TableCell>{issue.issue}</TableCell>
                            <TableCell>
                              <Badge variant={getSeverityColor(issue.severity)}>
                                {issue.severity}
                              </Badge>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </CardContent>
                </Card>
              </>
            ) : null}
          </TabsContent>

          {/* Performance Tab */}
          <TabsContent value="performance" className="space-y-4">
            {performanceLoading ? (
              <LoadingSkeleton />
            ) : performanceError ? (
              <ErrorDisplay error={performanceError as Error} onRetry={refetchPerformance} />
            ) : performanceData ? (
              <>
                <div className="grid gap-4 md:grid-cols-4">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Lighthouse Score</CardDescription>
                      <CardTitle className="text-4xl">{performanceData.lighthouseScore}</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Progress value={performanceData.lighthouseScore} className="mt-2" />
                      <p className="text-xs text-muted-foreground mt-2">Target: ≥85</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>LCP</CardDescription>
                      <CardTitle className="text-4xl">
                        {performanceData.coreWebVitals.lcp}s
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge
                        variant={
                          performanceData.coreWebVitals.lcp < 2.5 ? 'default' : 'destructive'
                        }
                      >
                        {performanceData.coreWebVitals.lcp < 2.5 ? 'Good' : 'Needs Work'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: &lt;2.5s</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>FID</CardDescription>
                      <CardTitle className="text-4xl">
                        {performanceData.coreWebVitals.fid}ms
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge
                        variant={
                          performanceData.coreWebVitals.fid < 100 ? 'default' : 'destructive'
                        }
                      >
                        {performanceData.coreWebVitals.fid < 100 ? 'Good' : 'Needs Work'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: &lt;100ms</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>CLS</CardDescription>
                      <CardTitle className="text-4xl">
                        {performanceData.coreWebVitals.cls}
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge
                        variant={
                          performanceData.coreWebVitals.cls < 0.1 ? 'default' : 'destructive'
                        }
                      >
                        {performanceData.coreWebVitals.cls < 0.1 ? 'Good' : 'Needs Work'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: &lt;0.1</p>
                    </CardContent>
                  </Card>
                </div>

                <Card>
                  <CardHeader>
                    <CardTitle>Performance Budget Status</CardTitle>
                    <CardDescription>Resource usage against defined budgets</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {Object.entries(performanceData.budgetStatus).map(([key, value]) => (
                      <div key={key}>
                        <div className="flex justify-between text-sm mb-2">
                          <span className="capitalize">{key}</span>
                          <span className="font-medium">
                            {value.current} / {value.budget} {value.unit}
                          </span>
                        </div>
                        <Progress value={(value.current / value.budget) * 100} />
                      </div>
                    ))}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Slowest Pages</CardTitle>
                    <CardDescription>
                      Pages with highest load times requiring optimization
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Page</TableHead>
                          <TableHead className="text-right">Load Time</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {performanceData.slowestPages.map((page, idx) => (
                          <TableRow key={idx}>
                            <TableCell className="font-mono text-sm">{page.page}</TableCell>
                            <TableCell className="text-right">
                              <Badge variant={page.loadTime > 2.5 ? 'destructive' : 'secondary'}>
                                {page.loadTime}s
                              </Badge>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </CardContent>
                </Card>
              </>
            ) : null}{' '}
          </TabsContent>

          {/* E2E Coverage Tab */}
          <TabsContent value="e2e" className="space-y-4">
            {e2eLoading ? (
              <LoadingSkeleton />
            ) : e2eError ? (
              <ErrorDisplay error={e2eError as Error} onRetry={refetchE2E} />
            ) : e2eData ? (
              <>
                <div className="grid gap-4 md:grid-cols-4">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Test Coverage</CardDescription>
                      <CardTitle className="text-4xl">{e2eData.coverage}%</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Progress value={e2eData.coverage} className="mt-2" />
                      <p className="text-xs text-muted-foreground mt-2">Target: ≥80%</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Pass Rate</CardDescription>
                      <CardTitle className="text-4xl">{e2eData.passRate}%</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge variant={e2eData.passRate >= 95 ? 'default' : 'destructive'}>
                        {e2eData.passRate >= 95 ? 'Excellent' : 'Needs Work'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: &gt;95%</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Flaky Rate</CardDescription>
                      <CardTitle className="text-4xl">{e2eData.flakyRate}%</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge variant={e2eData.flakyRate < 5 ? 'default' : 'destructive'}>
                        {e2eData.flakyRate < 5 ? 'Good' : 'High'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: &lt;5%</p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription>Execution Time</CardDescription>
                      <CardTitle className="text-4xl">{e2eData.executionTime}m</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Badge variant={e2eData.executionTime < 10 ? 'default' : 'destructive'}>
                        {e2eData.executionTime < 10 ? 'Fast' : 'Slow'}
                      </Badge>
                      <p className="text-xs text-muted-foreground mt-2">Target: &lt;10min</p>
                    </CardContent>
                  </Card>
                </div>

                <Card>
                  <CardHeader>
                    <CardTitle>Critical User Journeys</CardTitle>
                    <CardDescription>
                      Status of essential user flows - {e2eData.totalTests} total tests
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Journey</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead className="text-right">Duration (s)</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {e2eData.criticalJourneys.map((journey, idx) => (
                          <TableRow key={idx}>
                            <TableCell className="font-medium">{journey.name}</TableCell>
                            <TableCell>
                              <Badge className={getStatusColor(journey.status)}>
                                {journey.status.toUpperCase()}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-right font-mono">
                              {journey.duration}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Test Execution Summary</CardTitle>
                    <CardDescription>Last run: {formatDate(MOCK_DATA.e2e.lastRun)}</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    <div className="flex justify-between">
                      <span className="text-sm text-muted-foreground">Total Tests</span>
                      <span className="font-medium">{e2eData.totalTests}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm text-muted-foreground">Passed</span>
                      <span className="font-medium text-green-600">
                        {Math.round((e2eData.passRate / 100) * e2eData.totalTests)}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm text-muted-foreground">Flaky</span>
                      <span className="font-medium text-yellow-600">
                        {Math.round((e2eData.flakyRate / 100) * e2eData.totalTests)}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm text-muted-foreground">Failed</span>
                      <span className="font-medium text-red-600">
                        {Math.round(
                          ((100 - e2eData.passRate - e2eData.flakyRate) / 100) * e2eData.totalTests
                        )}
                      </span>
                    </div>
                  </CardContent>
                </Card>
              </>
            ) : null}{' '}
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
