/**
 * #4446: the RAG pipeline's Embedding tab and queue sidebar read /admin/embedding/info and /metrics
 * through the shared hooks, with the API's field names.
 *
 * Before: the tab read `dimensions`, `totalRequests` and `totalFailures` (never sent: "—" and 0)
 * and multiplied an already-percentage failure rate by 100; the sidebar called `.toFixed` on a
 * `throughputPerSecond` the API never sent, and the error took the whole page down. The answers
 * here go through MSW with the bodies the API sends, so the schemas and hooks run as in the page.
 *
 * #4511: the sidebar's timing, health and orphan cleanup called routes the API does not have, and
 * the tab read vector-stats with names it never sent. They now answer on the real routes, with the
 * bodies taken from the local stack, and no request may reach the old ones.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  REAL_PIPELINE_HEALTH_BODY,
  REAL_PROCESSING_METRICS_BODY,
  REAL_VECTOR_STATS_BODY,
} from '@/__tests__/fixtures/admin-rag-pipeline-bodies';
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
      HttpResponse.json(REAL_VECTOR_STATS_BODY)
    ),
    http.get(`${API_BASE}/api/v1/admin/pdfs/metrics/processing`, () =>
      HttpResponse.json(REAL_PROCESSING_METRICS_BODY)
    ),
    http.get(`${API_BASE}/api/v1/admin/kb/pipeline/health`, () =>
      HttpResponse.json(REAL_PIPELINE_HEALTH_BODY)
    ),
    http.post(`${API_BASE}/api/v1/admin/pdfs/maintenance/cleanup-orphans`, () =>
      HttpResponse.json({ orphanedChunks: 4, orphanedVectors: 0 })
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

/** Every request the components make, as `METHOD path`. */
let requests: string[] = [];

describe('RAG pipeline embedding views (#4446, #4511)', () => {
  beforeEach(() => {
    globalRequestCache.clear();
    answerAll();
    requests = [];
    server.events.on('request:start', ({ request }) => {
      requests.push(`${request.method} ${new URL(request.url).pathname}`);
    });
  });

  afterEach(() => {
    server.events.removeAllListeners();
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

  it('the Embedding tab shows the row of a game with the vectors the API counts', async () => {
    renderWithClient(<EmbeddingTab />);

    expect(await screen.findByText('Vector Stats (11015 vectors, 128 games)')).toBeInTheDocument();
    const row = screen.getByText('Android: Netrunner').closest('tr');
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText('201')).toBeInTheDocument();
    expect(within(row as HTMLElement).getByText('100%')).toBeInTheDocument();
  });

  it('the queue sidebar shows the step timings and the stage health the API sends', async () => {
    renderWithClient(<QueueETASidebar />);

    // Per-step averages are in seconds: Extracting 19.02, Embedding 21.26.
    expect((await screen.findByText('Extracting')).nextElementSibling?.textContent).toBe('19s');
    expect(screen.getByText('Embedding').nextElementSibling?.textContent).toBe('21s');

    const extractStage = (await screen.findByText('Extract')).parentElement as HTMLElement;
    expect(within(extractStage).getByText('warning')).toBeInTheDocument();
    expect(screen.getByText('Generate')).toBeInTheDocument();

    expect(requests.filter(r => r.includes('/admin/pipeline/'))).toEqual([]);
  });

  it('the cleanup button calls the real route and shows what it removed', async () => {
    renderWithClient(<QueueETASidebar />);

    await userEvent.click(await screen.findByRole('button', { name: 'Cleanup orphans' }));

    expect(await screen.findByText('Removed 4 orphaned chunks')).toBeInTheDocument();
    expect(requests).toContain('POST /api/v1/admin/pdfs/maintenance/cleanup-orphans');
    expect(requests.filter(r => r.includes('/admin/pipeline/'))).toEqual([]);
  });
});
