import { describe, expect, it, vi } from 'vitest';

import { buildKbConnections } from '../buildKbConnections';

describe('buildKbConnections', () => {
  const handlers = {
    onChunksClick: vi.fn(),
    onReindexClick: vi.fn(),
    onPreviewClick: vi.fn(),
    onDownloadClick: vi.fn(),
  };

  it('returns 4 nav items in canonical order: Chunks, Reindex, Anteprima, Download', () => {
    const items = buildKbConnections({ chunkCount: 124 }, handlers);
    expect(items.map(i => i.label)).toEqual(['Chunks', 'Reindex', 'Anteprima', 'Download']);
  });

  it('shows chunk count when defined and > 0', () => {
    const items = buildKbConnections({ chunkCount: 124 }, handlers);
    expect(items[0].count).toBe(124);
  });

  it('disables Chunks when chunkCount is undefined', () => {
    const items = buildKbConnections({}, handlers);
    expect(items[0].disabled).toBe(true);
  });

  it('reindex/preview/download are pure action slots (no count)', () => {
    const items = buildKbConnections({ chunkCount: 1 }, handlers);
    expect(items[1].count).toBeUndefined();
    expect(items[2].count).toBeUndefined();
    expect(items[3].count).toBeUndefined();
  });
});
