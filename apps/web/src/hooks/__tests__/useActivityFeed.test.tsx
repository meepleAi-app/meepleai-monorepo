import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useActivityFeed } from '../useActivityFeed';

vi.mock('@/lib/api', () => ({
  api: {
    activity: {
      listActivity: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'pages.library.activityRail.fallback.agent': 'Nuovo agent creato',
        'pages.library.activityRail.fallback.chat': 'Nuova chat',
        'pages.library.activityRail.fallback.kbIndexed': 'Documento indicizzato',
        'pages.library.activityRail.fallback.play': 'Sessione',
        'pages.library.activityRail.fallback.removed': 'Rimosso dalla libreria',
      };
      return map[key] ?? key;
    },
  }),
}));

import { api } from '@/lib/api';

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useActivityFeed', () => {
  beforeEach(() => {
    vi.mocked(api.activity.listActivity).mockReset();
  });

  it('fetches with default limit=20', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({ success: true, items: [], count: 0 });
    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(api.activity.listActivity).toHaveBeenCalledWith({ limit: 20 });
  });

  it('adapts dto items to rail ActivityItem via adapter', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({
      success: true,
      items: [
        {
          id: '11111111-1111-1111-1111-111111111111',
          eventId: '22222222-2222-2222-2222-222222222222',
          eventType: 'agent.created',
          userId: '33333333-3333-3333-3333-333333333333',
          entityType: 'Agent',
          entityId: '44444444-4444-4444-4444-444444444444',
          title: 'Catan Tutor',
          timestamp: '2026-05-28T11:00:00+00:00',
          loggedAt: '2026-05-28T11:00:01+00:00',
          payloadVersion: 1,
        },
      ],
      count: 1,
    });
    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.items[0]).toMatchObject({
      kind: 'agent',
      entityTitle: 'Catan Tutor',
      timestamp: '2026-05-28T11:00:00+00:00',
    });
  });

  it('uses i18n fallback when title is null', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({
      success: true,
      items: [
        {
          id: '11111111-1111-1111-1111-111111111111',
          eventId: '22222222-2222-2222-2222-222222222222',
          eventType: 'kb.doc.indexed',
          userId: '33333333-3333-3333-3333-333333333333',
          entityType: 'PdfDocument',
          entityId: '44444444-4444-4444-4444-444444444444',
          title: null,
          timestamp: '2026-05-28T11:00:00+00:00',
          loggedAt: '2026-05-28T11:00:01+00:00',
          payloadVersion: 1,
        },
      ],
      count: 1,
    });
    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.items[0].entityTitle).toBe('Documento indicizzato');
  });

  it('forwards a custom limit', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({ success: true, items: [], count: 0 });
    renderHook(() => useActivityFeed(50), { wrapper });
    await waitFor(() => expect(api.activity.listActivity).toHaveBeenCalled());
    expect(api.activity.listActivity).toHaveBeenCalledWith({ limit: 50 });
  });

  it('exposes error from a failed query', async () => {
    vi.mocked(api.activity.listActivity).mockRejectedValue(new Error('500 server error'));
    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(Error);
  });
});
