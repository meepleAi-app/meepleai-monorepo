import { NextResponse } from 'next/server';

import { logger } from '@/lib/logger';
import { getPrometheusMetrics } from '@/lib/metrics/session-cache-metrics';

/**
 * Prometheus metrics endpoint for Next.js middleware monitoring
 *
 * Issue #3797: Exposes session cache metrics for monitoring:
 * - Cache hit/miss rates
 * - Session validation success/failure rates
 * - Timeout occurrences
 * - System uptime
 *
 * Scraped by Prometheus at: http://localhost:3000/metrics
 *
 * @returns Prometheus text format metrics
 */
export async function GET() {
  try {
    const metricsText = getPrometheusMetrics();

    return new NextResponse(metricsText, {
      status: 200,
      headers: {
        'Content-Type': 'text/plain; charset=utf-8',
        'Cache-Control': 'no-store, no-cache, must-revalidate',
      },
    });
  } catch (error) {
    logger.error('[metrics] Failed to generate Prometheus metrics:', error);

    return new NextResponse('# Error generating metrics\n', {
      status: 500,
      headers: {
        'Content-Type': 'text/plain; charset=utf-8',
      },
    });
  }
}

/**
 * Disable static optimization for this route
 * Metrics must be generated on each request
 */
export const dynamic = 'force-dynamic';
