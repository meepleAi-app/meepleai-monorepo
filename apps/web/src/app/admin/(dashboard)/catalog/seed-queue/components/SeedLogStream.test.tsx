import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as streamHook from '../hooks/use-catalog-seed-stream';
import { SeedLogStream } from './SeedLogStream';

vi.mock('../hooks/use-catalog-seed-stream');

describe('SeedLogStream', () => {
  it('shows empty placeholder when no events have arrived', () => {
    vi.mocked(streamHook.useCatalogSeedStream).mockReturnValue({
      events: [],
      isConnected: true,
      clear: vi.fn(),
    });
    render(<SeedLogStream />);
    expect(screen.getByText(/No events yet/i)).toBeInTheDocument();
    expect(screen.getByText(/Connected/i)).toBeInTheDocument();
  });

  it('shows disconnected indicator when stream is offline', () => {
    vi.mocked(streamHook.useCatalogSeedStream).mockReturnValue({
      events: [],
      isConnected: false,
      clear: vi.fn(),
    });
    render(<SeedLogStream />);
    expect(screen.getByText(/Disconnected/i)).toBeInTheDocument();
  });

  it('renders events ordered by arrival', () => {
    vi.mocked(streamHook.useCatalogSeedStream).mockReturnValue({
      events: [
        {
          eventType: 'batch.started',
          occurredAt: '2026-06-04T12:34:56Z',
          batchId: 'abc',
          count: 10,
        },
        {
          eventType: 'seed.fetched',
          occurredAt: '2026-06-04T12:34:57Z',
          draftId: 'xyz',
          bggId: 13,
        },
      ],
      isConnected: true,
      clear: vi.fn(),
    });
    render(<SeedLogStream />);
    expect(screen.getByText(/BatchStarted/)).toBeInTheDocument();
    expect(screen.getByText(/SeedFetched/)).toBeInTheDocument();
  });

  it('filters to failed events only when checkbox is enabled', async () => {
    vi.mocked(streamHook.useCatalogSeedStream).mockReturnValue({
      events: [
        { eventType: 'seed.fetched', occurredAt: '2026-06-04T12:34:57Z', draftId: 'a' },
        { eventType: 'seed.failed', occurredAt: '2026-06-04T12:34:58Z', draftId: 'b' },
      ],
      isConnected: true,
      clear: vi.fn(),
    });
    render(<SeedLogStream />);
    await userEvent.click(screen.getByLabelText(/Filter failed only/i));
    expect(screen.queryByText(/SeedFetched/)).not.toBeInTheDocument();
    expect(screen.getByText(/SeedFailed/)).toBeInTheDocument();
  });

  it('clear button invokes the hook clear callback', async () => {
    const clearMock = vi.fn();
    vi.mocked(streamHook.useCatalogSeedStream).mockReturnValue({
      events: [{ eventType: 'seed.fetched', occurredAt: '2026-06-04T12:34:57Z', draftId: 'a' }],
      isConnected: true,
      clear: clearMock,
    });
    render(<SeedLogStream />);
    await userEvent.click(screen.getByRole('button', { name: /Clear/i }));
    expect(clearMock).toHaveBeenCalled();
  });
});
