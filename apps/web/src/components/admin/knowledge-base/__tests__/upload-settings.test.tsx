/**
 * #4446: the Processing Settings panel shows what the API and the embedding service report.
 *
 * Every value in it used to be written in the component, and several were wrong (e5-large at 1024
 * dimensions, 500 MB, 512-token chunks). The answers here go through MSW, so the schemas, the
 * clients and the hooks run as in the page. They carry values no version of the panel ever
 * contained — a 384-dimension model, Dutch and Portuguese, 20 MB, 5 sessions — so a value still
 * written in the component would not match.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { type ReactNode } from 'react';
import { beforeEach, describe, expect, it } from 'vitest';

import { server } from '@/__tests__/mocks/server';
import { globalRequestCache } from '@/lib/api/core/requestCache';

import { UploadSettings } from '../upload-settings';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const MB = 1024 * 1024;

const healthyInfo = {
  status: 'healthy',
  model: 'test/embedder-small',
  device: 'cpu',
  supportedLanguages: ['nl', 'pt'],
  dimension: 384,
  maxInputChars: 2048,
};

function answer(info: object | null, limits: object | null) {
  server.use(
    http.get(`${API_BASE}/api/v1/admin/embedding/info`, () =>
      info ? HttpResponse.json(info) : HttpResponse.json({ error: 'boom' }, { status: 500 })
    ),
    http.get(`${API_BASE}/api/v1/ingest/pdf/limits`, () =>
      limits ? HttpResponse.json(limits) : HttpResponse.json({ error: 'boom' }, { status: 500 })
    )
  );
}

function renderPanel() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return render(<UploadSettings />, { wrapper });
}

describe('UploadSettings (#4446)', () => {
  beforeEach(() => {
    globalRequestCache.clear();
  });

  it('shows the model, dimensions, languages, limit and sessions it is given', async () => {
    answer(healthyInfo, { maxFileSizeBytes: 20 * MB, maxConcurrentChunkedUploads: 5 });

    renderPanel();

    expect(await screen.findByText('test/embedder-small')).toBeInTheDocument();
    expect(screen.getByText('384 dimensions')).toBeInTheDocument();
    expect(screen.getByText('Dutch')).toBeInTheDocument();
    expect(screen.getByText('Portuguese')).toBeInTheDocument();
    expect(await screen.findByText('20 MB')).toBeInTheDocument();
    expect(screen.getByText('5 per user')).toBeInTheDocument();
  });

  it('has no Chunking row: the chunk size is chosen per document', async () => {
    answer(healthyInfo, { maxFileSizeBytes: 20 * MB, maxConcurrentChunkedUploads: 5 });

    renderPanel();

    await screen.findByText('test/embedder-small');
    expect(screen.queryByText('Chunking')).not.toBeInTheDocument();
    expect(screen.queryByText(/tokens/)).not.toBeInTheDocument();
  });

  it('shows the chunked upload threshold of the upload zone', async () => {
    answer(healthyInfo, { maxFileSizeBytes: 20 * MB, maxConcurrentChunkedUploads: 5 });

    renderPanel();

    expect(await screen.findByText('> 10 MB')).toBeInTheDocument();
  });

  it('with the embedding service down it says so instead of showing a model', async () => {
    answer(
      { ...healthyInfo, status: 'unavailable', model: null, device: null, supportedLanguages: [] },
      { maxFileSizeBytes: 20 * MB, maxConcurrentChunkedUploads: 5 }
    );

    renderPanel();

    expect(
      await screen.findByText('Not available: embedding service unavailable')
    ).toBeInTheDocument();
    expect(screen.queryByText('Dutch')).not.toBeInTheDocument();
    // The dimension is the API's, known even when the service is down.
    expect(screen.getByText('384 dimensions')).toBeInTheDocument();
  });

  it('when neither endpoint answers no row shows a number', async () => {
    answer(null, null);

    renderPanel();

    expect(
      await screen.findByText('Not available: the embedding service info could not be read')
    ).toBeInTheDocument();
    expect(screen.queryByText(/dimensions/)).not.toBeInTheDocument();
    // Supported languages, Max File Size and Concurrent Sessions.
    expect(await screen.findAllByText('Not available')).toHaveLength(3);
  });

  // #4513: the Badge these come from used to hardcode dark text with no background on its
  // `outline` variant, unreadable inside the dark admin shell. jsdom applies no Tailwind CSS, so
  // this checks the classes: text and border from the theme tokens, no fixed palette colour.
  it('the Read-only and Pipeline Steps badges take their colours from the theme tokens', async () => {
    answer(healthyInfo, { maxFileSizeBytes: 20 * MB, maxConcurrentChunkedUploads: 5 });

    const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } });
    render(
      <div data-admin-shell data-theme="dark">
        <QueryClientProvider client={client}>
          <UploadSettings />
        </QueryClientProvider>
      </div>
    );

    await screen.findByText('test/embedder-small');
    for (const label of ['Read-only', 'Upload', 'Extract', 'Chunk', 'Embed', 'Index']) {
      const badge = screen.getByText(label);
      expect(badge).toHaveClass('text-foreground');
      expect(badge.className).not.toMatch(/(?:text|border)-slate-\d/);
    }
  });
});
