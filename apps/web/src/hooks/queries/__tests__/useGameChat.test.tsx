/**
 * useGameChat — unit tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 */
import { renderHook, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

import { qaStream } from '@/lib/api/clients/chatClient';
import { useGameChat } from '../useGameChat';

async function* mockStream(events: Array<{ type: number; data: unknown }>) {
  for (const e of events) yield e;
}

const sampleCitation = {
  documentId: 'd1',
  pageNumber: 12,
  snippet: 'Ogni potere…',
  relevanceScore: 0.95,
  copyrightTier: 'full' as const,
};

const happyEvents = [
  { type: 7, data: 'Sì, ' },
  { type: 7, data: 'ogni potere si attiva ogni volta.' },
  { type: 4, data: { confidence: 0.92, Citations: [sampleCitation] } },
];

describe('useGameChat', () => {
  beforeEach(() => {
    vi.mocked(qaStream).mockReset();
  });

  it('starts with empty messages and tutor agent', () => {
    const { result } = renderHook(() => useGameChat('wingspan'));
    expect(result.current.messages).toEqual([]);
    expect(result.current.currentAgent).toBe('tutor');
    expect(result.current.isLoading).toBe(false);
  });

  it('ask appends user + agent messages on success', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('posso usare potere?');
    });
    expect(result.current.messages).toHaveLength(2);
    expect(result.current.messages[0].role).toBe('user');
    expect(result.current.messages[0].content).toBe('posso usare potere?');
    expect(result.current.messages[1].role).toBe('agent');
    expect(result.current.messages[1].content).toBe('Sì, ogni potere si attiva ogni volta.');
    expect(result.current.messages[1].overallConfidence).toBe(0.92);
    expect(result.current.messages[1].citations).toHaveLength(1);
    expect(result.current.messages[1].isLowQuality).toBe(false);
  });

  it('derives isLowQuality=true when confidence < 0.70', async () => {
    const lowConfEvents = [
      { type: 7, data: 'Non sono certo.' },
      { type: 4, data: { confidence: 0.42, Citations: [sampleCitation] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(lowConfEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => { await result.current.ask('edge?'); });
    expect(result.current.messages[1].isLowQuality).toBe(true);
    expect(result.current.messages[1].outOfContext).toBe(false);
  });

  it('derives outOfContext=true when no citations + confidence < 0.30', async () => {
    const oocEvents = [
      { type: 7, data: 'Non ho informazioni.' },
      { type: 4, data: { confidence: 0.0, Citations: [] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(oocEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => { await result.current.ask('tg?'); });
    expect(result.current.messages[1].outOfContext).toBe(true);
    expect(result.current.messages[1].citations).toBeUndefined();
  });

  it('isLoading transitions correctly during ask', async () => {
    let releaseStream: () => void = () => {};
    const slowStream = async function* () {
      await new Promise<void>(r => { releaseStream = r; });
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(slowStream() as any);
    const { result } = renderHook(() => useGameChat('wingspan'));

    act(() => { void result.current.ask('q'); });
    await waitFor(() => expect(result.current.isLoading).toBe(true));

    await act(async () => { releaseStream(); });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it('isError true when stream throws', async () => {
    const errorStream = async function* () {
      throw new Error('500');
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(errorStream() as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      try { await result.current.ask('q'); } catch { /* expected */ }
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('switchAgent updates currentAgent', () => {
    const { result } = renderHook(() => useGameChat('wingspan'));
    act(() => { result.current.switchAgent('arbitro'); });
    expect(result.current.currentAgent).toBe('arbitro');
  });

  it('switchAgent does NOT clear message history', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => { await result.current.ask('q'); });
    expect(result.current.messages).toHaveLength(2);
    act(() => { result.current.switchAgent('arbitro'); });
    expect(result.current.messages).toHaveLength(2);
  });

  it('initialAgent overrides default tutor', () => {
    const { result } = renderHook(() => useGameChat('wingspan', 'arbitro'));
    expect(result.current.currentAgent).toBe('arbitro');
  });
});
