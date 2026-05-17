/**
 * useGameChat — unit tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 */
import { renderHook, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
    },
  },
}));

import { qaStream } from '@/lib/api/clients/chatClient';
import { api } from '@/lib/api';
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
    vi.mocked(api.chat.getThreadsByGame).mockReset();
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([]);
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

  // ─── G2 hydrate tests ───────────────────────────────

  const sampleThreadMessage = (role: string, content: string, ts: string) => ({
    content,
    role,
    timestamp: ts,
  });

  const sampleThread = (id: string, lastMessageAt: string | null, messages: any[] = []) => ({
    id,
    gameId: 'wingspan',
    agentId: null,
    agentType: null,
    title: 'Test thread',
    createdAt: '2026-05-10T00:00:00Z',
    lastMessageAt,
    messageCount: messages.length,
    messages,
  });

  it('hydrates messages from latest thread on mount', async () => {
    const thread = sampleThread('thread-1', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'old question', '2026-05-10T09:00:00Z'),
      sampleThreadMessage('agent', 'old answer', '2026-05-10T09:01:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toHaveLength(2);
    expect(result.current.messages[0].content).toBe('old question');
    expect(result.current.messages[0].isHistorical).toBe(true);
    expect(result.current.chatThreadId).toBe('thread-1');
    expect(result.current.hasHistoricalMessages).toBe(true);
  });

  it('selects most recent thread when multiple exist (lastMessageAt desc)', async () => {
    const older = sampleThread('thread-old', '2026-05-09T10:00:00Z', [
      sampleThreadMessage('user', 'older', '2026-05-09T10:00:00Z'),
    ]);
    const newer = sampleThread('thread-new', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'newer', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([older, newer]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.chatThreadId).toBe('thread-new');
    expect(result.current.messages[0].content).toBe('newer');
  });

  it('starts empty when no threads exist (no error)', async () => {
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toEqual([]);
    expect(result.current.chatThreadId).toBeNull();
    expect(result.current.hasHistoricalMessages).toBe(false);
    expect(result.current.isError).toBe(false);
  });

  it('silent fail when getThreadsByGame rejects', async () => {
    vi.mocked(api.chat.getThreadsByGame).mockRejectedValueOnce(new Error('500'));
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toEqual([]);
    expect(result.current.isError).toBe(false);
  });

  it('passes chatId in subsequent qaStream calls after hydrate', async () => {
    const thread = sampleThread('thread-existing', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'old', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);

    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.chatThreadId).toBe('thread-existing'));

    await act(async () => { await result.current.ask('new question'); });

    expect(qaStream).toHaveBeenCalledWith(expect.objectContaining({
      chatId: 'thread-existing',
    }));
  });

  it('preserves messages across remount (audit trigger A)', async () => {
    const thread = sampleThread('thread-1', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'cached', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([thread]);

    const { result, unmount } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.messages.length).toBeGreaterThan(0));

    unmount();

    const { result: result2 } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result2.current.messages.length).toBeGreaterThan(0));
    expect(result2.current.messages[0].content).toBe('cached');
  });
});
