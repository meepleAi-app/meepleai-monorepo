/**
 * #4446: the RAG pipeline's Embedding tab and queue sidebar read /admin/embedding/info and /metrics
 * through the shared hooks, with the API's field names.
 *
 * Before: the tab read `dimensions`, `totalRequests` and `totalFailures` (never sent: "—" and 0)
 * and multiplied an already-percentage failure rate by 100; the sidebar called `.toFixed` on a
 * `throughputPerSecond` the API never sent, and the error took the whole page down. The answers
 * here go through MSW with the bodies the API sends, so the schemas and hooks run as in the page.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { type ReactNode } from 'react';
import { beforeEach, describe, expect, it } from 'vitest';

import { server } from '@/__tests__/mocks/server';
import { globalRequestCache } from '@/lib/api/core/requestCache';

import { EmbeddingTab } from '../embedding-tab';
import { QueueETASidebar } from '../queue-eta-sidebar';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const INFO = {
  status: 'healthy',
  model: 'test/embedder-small',
  device: 'cpu',
  supportedLanguages: ['en'],
  dimension: 384,
  maxInputChars: 2048,
};

// Shape of the API's body (AdminEmbeddingEndpoints.ParsePrometheusMetrics): failureRate is a %.
const METRICS = {
  requestsTotal: 40,
  failuresTotal: 3,
  durationMsSum: 2000,
  totalCharsSum: 9000,
  avgDurationMs: 50,
  failureRate: 7.5,
};

function answerAll() {
  server.use(
    http.get(`${API_BASE}/api/v1/admin/embedding/info`, () => HttpResponse.json(INFO)),
    http.get(`${API_BASE}/api/v1/admin/embedding/metrics`, () => HttpResponse.json(METRICS)),
    http.get(`${API_BASE}/api/v1/admin/kb/vector-stats`, () =>
      HttpResponse.json({ games: [], totalVectors: 0, totalDocuments: 0 })
    ),
    http.get(`${API_BASE}/api/v1/admin/pipeline/metrics`, () =>
      HttpResponse.json({ error: 'not found' }, { status: 404 })
    ),
    http.get(`${API_BASE}/api/v1/admin/pipeline/health`, () =>
      HttpResponse.json({ error: 'not found' }, { status: 404 })
    ),
    http.get(`${API_BASE}/api/v1/admin/queue/config`, () =>
      HttpResponse.json({ isPaused: false, maxConcurrentWorkers: 3 })
    )
  );
}

function renderWithClient(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/** InfoCell and MetricCell render the label and then the value. */
function cellValue(label: string): string {
  return screen.getByText(label).nextElementSibling?.textContent ?? '';
}

describe('RAG pipeline embedding views (#4446)', () => {
  beforeEach(() => {
    globalRequestCache.clear();
    answerAll();
  });

  it('the Embedding tab shows the dimension the API sends', async () => {
    renderWithClient(<EmbeddingTab />);

    expect(await screen.findByText('Service Info')).toBeInTheDocument();
    expect(cellValue('Model')).toBe('test/embedder-small');
    expect(cellValue('Dimensions')).toBe('384');
  });

  it('the Embedding tab shows the metrics under the API names, the rate as sent', async () => {
    renderWithClient(<EmbeddingTab />);

    await screen.findByText('Metrics');
    expect(cellValue('Total Requests')).toBe('40');
    expect(cellValue('Failures')).toBe('3');
    expect(cellValue('Failure Rate')).toBe('7.5%');
    expect(cellValue('Avg Duration')).toBe('50ms');
  });

  it('the queue sidebar renders the embedding metrics instead of crashing', async () => {
    renderWithClient(<QueueETASidebar />);

    expect(await screen.findByText('50ms')).toBeInTheDocument();
    expect(screen.getByText('7.5%')).toBeInTheDocument();
    expect(screen.getByText('384')).toBeInTheDocument();
    expect(screen.queryByText('Throughput')).not.toBeInTheDocument();
  });
});
